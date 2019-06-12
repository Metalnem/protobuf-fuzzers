#!/bin/sh
set -eux

docker build -t build-wpf-fuzz .
docker run --rm -v $(pwd)/corpus:/app/corpus --name run-wpf-fuzz build-wpf-fuzz
