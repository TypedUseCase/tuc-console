namespace MF.TucConsole.Command

open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console
open MF.Domain

[<RequireQualifiedAccess>]
module Tuc =
    let check: ExecuteCommand = fun (input, output) ->
        ExitCode.Success

    let generate: ExecuteCommand = fun (input, output) ->
        let domain = (input, output) |> Input.getDomain
        let domainResult = Some domain |> checkDomain (input, output)

        match input, domainResult with
        | _, Error error -> error |> showParseDomainError output
        | _, Ok domainTypes ->
            // todo ...

            failwith "Not implemented yet ..."

        ExitCode.Success
