#!/bin/sh
set -eux

docker build -t libfuzzer-proto-dotnet .
docker container create --name extract-libfuzzer-proto-dotnet libfuzzer-proto-dotnet
docker container cp extract-libfuzzer-proto-dotnet:/app/libprotobuf-mutator/build/src/libfuzzer-proto-dotnet/libfuzzer-proto-dotnet .
docker container rm -f extract-libfuzzer-proto-dotnet
tar -czvf libfuzzer-proto-dotnet.tar.gz libfuzzer-proto-dotnet
