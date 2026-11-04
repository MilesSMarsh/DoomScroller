#!/usr/bin/env bash
# Runs the server tests headlessly; non-zero exit on failure.
# Uses local `dotnet` if the ASP.NET Core runtime is installed (Arch: pacman -S aspnet-runtime
# aspnet-targeting-pack); otherwise runs inside the official .NET 10 SDK container.
set -euo pipefail
cd "$(dirname "$0")/../Server"
if dotnet --list-runtimes 2>/dev/null | grep -q Microsoft.AspNetCore.App; then
  exec dotnet test --nologo -v q "$@"
fi
echo "ASP.NET Core runtime not installed locally; running tests in Docker." >&2
exec docker run --rm -v "$PWD":/work -w /work -e DOTNET_CLI_HOME=/tmp -e HOME=/tmp \
  --user "$(id -u):$(id -g)" mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test --nologo -v q -p:BaseIntermediateOutputPath=/tmp/obj/ "$@"
