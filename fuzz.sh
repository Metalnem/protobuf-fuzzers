#!/bin/sh
set -eux

docker build -t build-roslyn-fuzz .
docker run --rm -v "$(pwd)"/corpus:/app/corpus --name run-roslyn-fuzz build-roslyn-fuzz
