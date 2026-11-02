#!/bin/sh
# Game-free Jlib contract checks. Extra arguments are forwarded, for example:
#   Jlib/tools/test-contract.sh --inspect path/net48/Jlib.dll path/netstandard2.1/Jlib.dll
#   Jlib/tools/test-contract.sh --inspect-package artifacts/Jlib.zip
set -eu
cd "$(dirname "$0")/../.."
dotnet run --project Jlib.Tests/Jlib.Tests.csproj -c Release -- "$@"
