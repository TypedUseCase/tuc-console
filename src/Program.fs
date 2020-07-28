open System
open System.IO
open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console
open MF.Domain

[<EntryPoint>]
let main argv =
    consoleApplication {
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion

        command "domain:check" {
            Description = "Checks domain by simply parse it and write the result to the stdout."
            Help = None
            Arguments = [
                Argument.domains
            ]
            Options = [
                Option.noValue "only-parse" (Some "p") "Whether to just parse domain and dump a results."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                //let onlyParse = input |> Input.isOptionValueSet "only-parse"

                let parsedDomains =
                    (input, output)
                    |> Input.getDomains
                    |> List.map (Parser.parse output)

                match input with
                | Input.HasOption "only-parse" _ ->
                    parsedDomains
                    |> List.iter (Dump.parsedDomain output)
                | _ ->
                    ()

                ExitCode.Success
        }

        command "tuc:generate" {
            Description = ""
            Help = None
            Arguments = [
                Argument.required "tuc" "Path to tuc file containing a Typed Use-Case definition."
                Argument.domains
            ]
            Options = [
                Option.optional "output" (Some "o") "Path to an output PlantUML file. (If not set, it will be a input file name with .puml" None
                Option.noValue "watch" (Some "w") "Whether to watch a tuc file, to change an output file on the fly."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let domains = (input, output) |> Input.getDomains

                let parsedDomains =
                    domains
                    |> List.map (Parser.parse output)

                ExitCode.Success
        }

        command "about" {
            Description = "Displays information about the current project."
            Help = None
            Arguments = []
            Options = []
            Initialize = None
            Interact = None
            Execute = fun (_input, output) ->
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
        }
    }
    |> run argv
