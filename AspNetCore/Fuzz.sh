#!/bin/sh
set -eux

docker build -t build-aspnetcore-fuzz .
docker run --rm --name run-aspnetcore-fuzz build-aspnetcore-fuzz
