# Options: Sidecar / InitContainer

## Docker up

```shell
docker buildx build --platform linux/amd64 \
-t qrsln/vault-option-init-container:1.0.0 \
-f ./docker/Dockerfile-option-init-container . \
--push

docker buildx build --platform linux/amd64 \
-t qrsln/vault-option-sidecar:1.0.0 \
-f ./docker/Dockerfile-option-sidecar . \
--push

```

## Option: Init Container

```shell
kubectl delete sa vault-option-init-container-sa -n infra

kubectl delete deployment vault-option-init-container -n infra
kubectl apply -f options/vault-option-init-container.yaml -n infra
```

## Option: Sidecar

```shell
kubectl delete sa vault-option-sidecar-sa -n infra

kubectl delete deployment vault-option-sidecar -n infra
kubectl apply -f options/vault-option-sidecar.yaml -n infra
```

### Check in Pod

```shell
cat /vault/env/secrets.json
```

### Policy

```json
{
  "name": "VaultOptionsTesting",
  "rules": [
    {
      "effect": 1,
      "subject": {
        "namespace": "infra",
        "serviceAccount": "vault-option-init-container-sa",
        "requiredPodLabels": {}
      },
      "resource": {
        "path": "db/**"
      },
      "capabilities": [
        "read"
      ]
    },
    {
      "effect": 1,
      "subject": {
        "namespace": "infra",
        "serviceAccount": "vault-test-sidecar-sa",
        "requiredPodLabels": {}
      },
      "resource": {
        "path": "db/**"
      },
      "capabilities": [
        "read"
      ]
    }
  ]
}
```

### Usage on .Net app

```csharp
builder.Configuration
    .AddJsonFile("/vault/env/secrets.json",
        optional: false,
        reloadOnChange: true);

# Check
var pwd = builder.Configuration["POSTGRES_PASSWORD"];

```

## Helm ile Kullanım Örneği (values.yaml)

```yaml
vaultSecrets:
  POSTGRES_PASSWORD: postgres-password
  POSTGRES_USER: postgres-user
  NOT_EXIST_TEST: not-exist

# Template'te:
env:
  - name: VAULT_API
    value: "http://vault-api:80"
  - name: VAULT_SECRETS_LIST
    value: "{{ range $key, $path := .Values.vaultSecrets }}{{ $key }}={{ $path }},{{ end }}"
  - name: VAULT_SECRETS_LIST
    value: "{{ range $key, $path := .Values.vaultSecrets }}{{ $key }}={{ $path }},{{ end }}"
```