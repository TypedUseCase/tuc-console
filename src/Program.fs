open System
open System.IO
open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console
open MF.Domain
open ErrorHandling

(*
    [
        Example.email
        Example.phone
        Example.interactionEvent
        Example.indentityMatchingSet

        Example.personId
        Example.person

        Example.commandResult

        Example.genericService
        Example.interactionCollector
        Example.personIdentificationEngine
        Example.personAggregate

        Example.interactionCollectorStream
    ]
    |> List.iter (Dump.parsedType output)
 *)

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

                let checkDomain domain =
                    let domain =
                        match domain with
                        | Some domain -> domain
                        | _ -> (input, output) |> Input.getDomain

                    let parsedDomains =
                        match domain with
                        | SingleFile file -> [ file ]
                        | Dir (_, files) -> files
                        |> List.map (Parser.parse output)

                    let showUnresolvedTypeError unresolvedTypes =
                        unresolvedTypes
                        |> List.map (TypeName.value >> List.singleton)
                        |> output.Options (sprintf "Unresolved types [%d]:" (unresolvedTypes |> List.length))

                        output.Error "You have to solve unresolved types first.\n"

                    match input with
                    | Input.HasOption "count" _ ->
                        let resolvedTypesResult =
                            parsedDomains
                            |> Resolver.resolve output

                        match resolvedTypesResult with
                        | Ok resolvedTypes ->
                            resolvedTypes
                            |> List.length
                            |> sprintf "Count of resolved types: <c:magenta>%d</c>"
                            |> output.Message

                            output.NewLine()
                        | Error unresolvedTypes -> showUnresolvedTypeError unresolvedTypes

                    | Input.HasOption "only-parse" _ ->
                        parsedDomains
                        |> List.iter (Dump.parsedDomain output)

                    | _ ->
                        let resolvedTypesResult =
                            parsedDomains
                            |> tee (fun _ -> output.Section "Checker -> parse types")
                            |> Resolver.resolve output

                        match resolvedTypesResult with
                        | Ok resolvedTypes ->
                            resolvedTypes
                            |> tee (fun _ ->
                                output.NewLine()
                                output.Section <| sprintf "Dump resolved types [%d]" (resolvedTypes |> List.length)
                            )
                            |> List.iter (Dump.parsedType output)

                        | Error unresolvedTypes -> showUnresolvedTypeError unresolvedTypes

                match input with
                | Input.HasOption "watch" _ ->
                    let path, includeSubDirs =
                        match domain with
                        | SingleFile file -> file, false
                        | Dir (dir, _) -> dir, true

                    use watcher =
                        new FileSystemWatcher(
                            Path = path,
                            Filter = "*.fsx",
                            EnableRaisingEvents = true,
                            IncludeSubdirectories = includeSubDirs
                        )
                    watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.LastWrite
                    watcher.SynchronizingObject <- null

                    let notifyWatch () =
                        path
                        |> sprintf " <c:dark-yellow>! Watching domain at %A</c> (Press <c:yellow>ctrl + c</c> to stop) ...\n"
                        |> output.Message

                    let checkOnWatch event =
                        if output.IsDebug() then output.Message <| sprintf "[Watch] Source %s." event

                        output.Message "Checking ...\n"

                        try checkDomain None
                        with e -> output.Error e.Message

                        notifyWatch ()

                    watcher.Changed.Add(fun _ -> checkOnWatch "changed")
                    watcher.Created.Add(fun _ -> checkOnWatch "created")
                    watcher.Deleted.Add(fun _ -> checkOnWatch "deleted")
                    watcher.Renamed.Add(fun _ -> checkOnWatch "renamed")

                    checkOnWatch "init"

                    while true do ()
                | _ ->
                    checkDomain (Some domain)

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

                let parsedDomains =
                    match domain with
                    | SingleFile file -> [ file ]
                    | Dir (_, files) -> files
                    |> List.map (Parser.parse output)

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
