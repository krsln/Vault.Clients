import os
import sys
import time
import logging
import tempfile
from typing import Optional
import requests

# -------------------------------------------------------------------
# Configuration
# -------------------------------------------------------------------

VAULT_API = os.getenv("VAULT_API", "http://vault-api:80").rstrip("/")
JWT_PATH = "/var/run/secrets/kubernetes.io/serviceaccount/token"
OUT_FILE = "/vault-env/env-vars"

SECRETS_LIST = os.getenv("VAULT_SECRETS_LIST", "")
RETRY = int(os.getenv("VAULT_RETRY", "3"))
TIMEOUT = int(os.getenv("VAULT_HTTP_TIMEOUT", "5"))

AUTH_ENDPOINT = "/api/auth/k8s"
SECRET_ENDPOINT = "/api/v1/secret/read"

# -------------------------------------------------------------------
# Logging
# -------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S",
)

log = logging.getLogger("vault-env")

# -------------------------------------------------------------------
# Helpers
# -------------------------------------------------------------------

def fatal(msg: str) -> None:
    log.error(msg)
    sys.exit(1)

def read_jwt() -> str:
    if not os.path.exists(JWT_PATH):
        fatal(f"ServiceAccount JWT not found at {JWT_PATH}")

    with open(JWT_PATH, "r") as f:
        token = f.read().strip()

    if not token:
        fatal("ServiceAccount JWT is empty")

    return token

def authenticate(jwt: str) -> str:
    url = f"{VAULT_API}{AUTH_ENDPOINT}"

    try:
        r = requests.post(
            url,
            json={"jwt": jwt},
            timeout=TIMEOUT,
        )
        r.raise_for_status()
    except requests.RequestException as e:
        fatal(f"Vault authentication failed: {e}")

    token = r.json().get("token")
    if not token:
        fatal("Vault auth response does not contain token")

    log.info("Vault authentication successful")
    return token

def fetch_secret(token: str, path: str) -> Optional[str]:
    url = f"{VAULT_API}{SECRET_ENDPOINT}/{path}"

    try:
        r = requests.post(
            url,
            headers={"Authorization": f"Bearer {token}"},
            timeout=TIMEOUT,
        )
        
    except requests.RequestException as e:
        raise RuntimeError(f"Network error: {e}") from e

    if r.status_code == 403:
        log.warning("Access denied for secret path: %s", path)
        return None

    if r.status_code == 404:
        log.warning("Secret not found: %s", path)
        return None

    r.raise_for_status()
    return r.json().get("value")

def write_env_file(pairs: list[tuple[str, str]]) -> None:
    os.makedirs(os.path.dirname(OUT_FILE), exist_ok=True)

    with tempfile.NamedTemporaryFile("w", delete=False, dir=os.path.dirname(OUT_FILE)) as tmp:
        for name, value in pairs:
            tmp.write(f'export {name}="{value}"\n')

    os.replace(tmp.name, OUT_FILE)
    log.info("Environment file written: %s", OUT_FILE)

# -------------------------------------------------------------------
# Main
# -------------------------------------------------------------------

def main() -> None:
    if not SECRETS_LIST:
        log.info("No secrets defined, exiting")
        return

    jwt = read_jwt()
    token = authenticate(jwt)

    exported = []

    for pair in SECRETS_LIST.split(","):
        if "=" not in pair:
            log.warning("Invalid secret definition: %s", pair)
            continue

        name, path = pair.split("=", 1)
        log.info("Fetching secret: %s", name)

        value = None
        for attempt in range(1, RETRY + 1):
            try:
                value = fetch_secret(token, path)
                break
            except Exception as e:
                log.warning(
                    "Retry %d/%d for %s failed: %s",
                    attempt,
                    RETRY,
                    name,
                    e,
                )
                time.sleep(2 ** attempt)

        if value is not None:
            exported.append((name, value))
            log.info("Secret loaded: %s", name)
        else:
            log.error("Secret skipped after retries: %s", name)

    if not exported:
        fatal("No secrets were successfully loaded")

    write_env_file(exported)

if __name__ == "__main__":
    main()
