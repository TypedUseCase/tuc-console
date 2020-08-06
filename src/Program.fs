open System
open System.IO
open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console
open MF.Domain
open ErrorHandling

[<EntryPoint>]
let main argv =
    consoleApplication {
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion

        command "domain:check" {
            Description = "Checks given domains."
            Help = None
            Arguments = [
                Argument.domain
            ]
            Options = [
                Option.noValue "only-parse" (Some "p") "Whether to just parse domain and dump a results."
                Option.noValue "count" (Some "c") "Whether to just show a count of results."
                Option.noValue "watch" (Some "w") "Whether to watch domains for changes (Press <c:yellow>ctrl + c</c> to stop)."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let domain = (input, output) |> Input.getDomain

                let execute domain =
                    match input with
                    | Input.HasOption "only-parse" _ ->
                        domain
                        |> parseDomain (input, output)
                        |> List.iter (Dump.parsedDomain output)

                    | _ ->
                        let domainResult = domain |> checkDomain (input, output)

                        match input, domainResult with
                        | _, Error error -> error |> showParseDomainError output

                        | Input.HasOption "count" _, Ok domainTypes ->
                            domainTypes
                            |> List.length
                            |> sprintf "Count of resolved types: <c:magenta>%d</c>"
                            |> output.Message

                            output.NewLine()

                        | _, Ok domainTypes ->
                            domainTypes
                            |> tee (fun _ ->
                                output.NewLine()
                                output.Section <| sprintf "Domain types [%d]" (domainTypes |> List.length)
                            )
                            |> List.iter (fun (DomainType domainType) -> domainType |> Dump.parsedType output)

                match input with
                | Input.HasOption "watch" _ ->
                    let path, watchSubdirs =
                        match domain with
                        | SingleFile file -> file, WatchSubdirs.No
                        | Dir (dir, _) -> dir, WatchSubdirs.Yes

                    (path, "*.fsx")
                    |> watch output watchSubdirs execute
                | _ ->
                    execute (Some domain)

                ExitCode.Success
        }

        command "tuc:generate" {
            Description = ""
            Help = None
            Arguments = [
                Argument.required "tuc" "Path to tuc file containing a Typed Use-Case definition."
                Argument.domain
            ]
            Options = [
                Option.optional "output" (Some "o") "Path to an output PlantUML file. (If not set, it will be a input file name with .puml" None
                Option.noValue "watch" (Some "w") "Whether to watch a tuc file, to change an output file on the fly."
            ]
            Initialize = None
            Interact = None
            Execute = fun (input, output) ->
                let domain = (input, output) |> Input.getDomain
                let domainResult = Some domain |> checkDomain (input, output)

                match input, domainResult with
                | _, Error error -> error |> showParseDomainError output
                | _, Ok domainTypes ->
                    // todo ...

                    failwith "Not implemented yet ..."

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
