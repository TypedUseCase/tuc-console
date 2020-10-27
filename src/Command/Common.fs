namespace Tuc.Console.Command

open MF.ConsoleApplication

type ExecuteCommand = IO -> ExitCode

[<RequireQualifiedAccess>]
module Common =
    open System

    let about: ExecuteCommand = fun (_input, output) ->
        let ``---`` = [ "------------------"; "----------------------------------------------------------------------------------------------" ]

        output.Table [ AssemblyVersionInformation.AssemblyProduct ] [
            [ "Description"; AssemblyVersionInformation.AssemblyDescription ]
            [ "Version"; AssemblyVersionInformation.AssemblyVersion ]

            ``---``
            [ "Environment" ]
            ``---``
            [ ".NET Core"; Environment.Version |> sprintf "%A" ]
            [ "Command Line"; Environment.CommandLine ]
            [ "Current Directory"; Environment.CurrentDirectory ]
            [ "Machine Name"; Environment.MachineName ]
            [ "OS Version"; Environment.OSVersion |> sprintf "%A" ]
            [ "Processor Count"; Environment.ProcessorCount |> sprintf "%A" ]

            ``---``
            [ "Git" ]
            ``---``
            [ "Branch"; AssemblyVersionInformation.AssemblyMetadata_gitbranch ]
            [ "Commit"; AssemblyVersionInformation.AssemblyMetadata_gitcommit ]
        ]

        ExitCode.Success
