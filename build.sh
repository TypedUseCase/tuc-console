#!/usr/bin/env bash

dotnet tool restore
# shellcheck disable=SC2068
dotnet fake build $@
