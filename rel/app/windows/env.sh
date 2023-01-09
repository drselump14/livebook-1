#!/bin/sh
set -euo pipefail

export MIX_ENV=prod
export MIX_TARGET=app
export ELIXIRKIT_APP_NAME=Livebook
export ELIXIRKIT_PROJECT_DIR=$PWD/../../..
export ELIXIRKIT_RELEASE_NAME=app
export ELIXIRKIT_CONFIGURATION=Release

target_dir="$PWD/bin/${ELIXIRKIT_APP_NAME}-${ELIXIRKIT_CONFIGURATION}"
build_args="--configuration ${ELIXIRKIT_CONFIGURATION} --output ${target_dir}"
