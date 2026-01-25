# Helm

```shell
helm dependency build ./helm/dummy
helm dependency update ./helm/dummy

helm upgrade --install vault-sdk-net8 ./helm/dummy -n infra --create-namespace -f ./helm/values-net8.yaml

helm uninstall vault-sdk-net8 -n infra
```