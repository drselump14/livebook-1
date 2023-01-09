#!/bin/bash
#
# Usage:
#
#     $ sh .github/scripts/app/build_macos.sh
#     $ open rel/app/macos/.build/LivebookInstall.dmg
#     $ open livebook://github.com/livebook-dev/livebook/blob/main/test/support/notebooks/basic.livemd
#     $ open ./test/support/notebooks/basic.livemd
#
# Note: This script builds the Mac installer. If you just want to test the Mac app locally, run:
#
#     $ cd rel/app/macos && ./run.sh
#
# See rel/app/macos/README.md for more information.
set -e

dir=$PWD
cd elixirkit/otp_bootstrap
. ./build_macos_universal.sh 25.1.2 1.1.1s
cd $dir

mix local.hex --force --if-missing
mix local.rebar --force --if-missing

export MIX_ENV=prod MIX_TARGET=app
mix deps.get --only prod

cd rel/app/macos
./build_dmg.sh
