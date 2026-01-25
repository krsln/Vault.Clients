# Docker up

```shell
docker buildx build --platform linux/amd64 \
-t registry.bidyno.com/test/vault-dummy-web:1.0.0 \
-f ./docker/Dockerfile-dummy-web . \
--push
 
docker buildx build --platform linux/amd64 \
-t registry.bidyno.com/test/vault-dummy-web-net8:1.0.0 \
-f ./docker/Dockerfile-dummy-web-sdk-net8 . \
--push
 

```