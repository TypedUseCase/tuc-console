TUC console
===========

[![Build Status](https://dev.azure.com/MortalFlesh/TucConsole/_apis/build/status/MortalFlesh.TucConsole)](https://dev.azure.com/MortalFlesh/TucConsole/_build/latest?definitionId=1)
[![Build Status](https://api.travis-ci.com/MortalFlesh/tuc-console.svg?branch=master)](https://travis-ci.com/MortalFlesh/tuc-console)

## Run statically

First compile
```sh
fake build target release
```

Then run
```sh
dist/tuc-console help
```

List commands
```sh
dist/tuc-console list
```
     ______  __  __  _____       _____                        __
    /_  __/ / / / / / ___/      / ___/ ___   ___   ___ ___   / / ___
     / /   / /_/ / / /__       / /__  / _ \ / _ \ (_-</ _ \ / / / -_)
    /_/    \____/  \___/       \___/  \___//_//_//___/\___//_/  \__/


    ==================================================================

    Usage:
        command [options] [--] [arguments]

    Options:
        -h, --help            Display this help message
        -q, --quiet           Do not output any message
        -V, --version         Display this application version
        -n, --no-interaction  Do not ask any interactive question
        -v|vv|vvv, --verbose  Increase the verbosity of messages

    Available commands:
        about  Displays information about the current project.
        help   Displays help for a command
        list   Lists commands

---
### Development

First run `dotnet build` or `dotnet watch run`

List commands
```sh
bin/console list
```

Run tests locally
```sh
fake build target Tests
```
