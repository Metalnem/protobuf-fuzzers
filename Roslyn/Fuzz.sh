#!/bin/sh
set -eux

docker build -t build-roslyn-fuzz .
docker run --rm --name run-roslyn-fuzz build-roslyn-fuzz
