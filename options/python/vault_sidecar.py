import os
import sys
import time
import json
import hashlib
import logging
import tempfile
from typing import Optional
import requests

# ------------------------------------------------------------------
# Configuration
# ------------------------------------------------------------------

VAULT_API = os.getenv("VAULT_API", "http://vault-api:80").rstrip("/")
JWT_PATH = "/var/run/secrets/kubernetes.io/serviceaccount/token"
OUT_FILE = "/vault/env/secrets.json"

AUTH_ENDPOINT = "/api/auth/k8s"
SECRET_ENDPOINT = "/api/v1/secret/read"

SECRETS_LIST = os.getenv("VAULT_SECRETS_LIST", "")
RETRY = int(os.getenv("VAULT_RETRY", "3"))
REFRESH = int(os.getenv("VAULT_REFRESH_INTERVAL", "300"))
TIMEOUT = int(os.getenv("VAULT_HTTP_TIMEOUT", "5"))

# ------------------------------------------------------------------
# Logging
# ------------------------------------------------------------------

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S",
)

log = logging.getLogger("vault-secret-sync")

# ------------------------------------------------------------------
# Helpers
# ------------------------------------------------------------------

def read_jwt() -> str:
    if not os.path.exists(JWT_PATH):
        raise RuntimeError(f"JWT not found at {JWT_PATH}")

    with open(JWT_PATH, "r") as f:
        token = f.read().strip()

    if not token:
        raise RuntimeError("JWT is empty")

    return token

def authenticate(jwt: str) -> str:
    url = f"{VAULT_API}{AUTH_ENDPOINT}"

    r = requests.post(
        url,
        json={"jwt": jwt},
        timeout=TIMEOUT,
    )
    r.raise_for_status()

    token = r.json().get("token")
    if not token:
        raise RuntimeError("Vault auth response missing token")

    log.info("Vault authentication successful")
    return token

def fetch_secret(token: str, path: str) -> Optional[str]:
    encoded_path = requests.utils.quote(path, safe="")
    url = f"{VAULT_API}{SECRET_ENDPOINT}/{encoded_path}"
 
    try:
        r = requests.post(
            url,
            headers={"Authorization": f"Bearer {token}"},
            timeout=TIMEOUT,
        )
        
    except requests.RequestException as e:
        raise RuntimeError(f"Network error: {e}") from e

    if r.status_code == 403:
        log.warning("Access denied: %s", path)
        return None

    if r.status_code == 404:
        log.warning("Secret not found: %s", path)
        return None

    r.raise_for_status()
    return r.json().get("value")

def hash_obj(obj: dict) -> str:
    payload = json.dumps(obj, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()

def atomic_write_json(path: str, data: dict) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)

    with tempfile.NamedTemporaryFile(
        "w",
        delete=False,
        dir=os.path.dirname(path),
        encoding="utf-8",
    ) as tmp:
        json.dump(data, tmp, indent=2, sort_keys=True)

    os.replace(tmp.name, path)

# ------------------------------------------------------------------
# Main Loop
# ------------------------------------------------------------------

def main_loop() -> None:
    if not SECRETS_LIST:
        log.warning("No secrets defined, sleeping")
        time.sleep(3600)
        return

    last_hash: Optional[str] = None
    token: Optional[str] = None

    while True:
        try:
            if not token:
                jwt = read_jwt()
                token = authenticate(jwt)

            secrets: dict[str, str] = {}

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
                    except requests.HTTPError as e:
                        if e.response and e.response.status_code == 401:
                            log.warning("Token expired, re-authenticating")
                            token = None
                            break
                        log.warning(
                            "Retry %d/%d failed for %s",
                            attempt,
                            RETRY,
                            name,
                        )
                        time.sleep(2 ** attempt)

                secrets[name] = value or ""

            current_hash = hash_obj(secrets)

            if current_hash != last_hash:
                atomic_write_json(OUT_FILE, secrets)
                last_hash = current_hash
                log.info("Secrets synchronized")
            else:
                log.info("No secret changes detected")

        except Exception as e:
            log.error("Sync cycle failed: %s", e)

       
        if REFRESH <= 0:
            log.info("One-shot mode enabled, sleeping indefinitely")
            while True:
                time.sleep(86400)  # Or any large value to keep container alive

        time.sleep(REFRESH)

# ------------------------------------------------------------------
# Entry Point
# ------------------------------------------------------------------

if __name__ == "__main__":
    main_loop()
