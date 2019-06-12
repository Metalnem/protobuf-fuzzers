#!/bin/sh
set -eux

docker build -t build-wpf-fuzz .
docker run --rm --name run-wpf-fuzz build-wpf-fuzz
