namespace MF.TucConsole

module Console =
    open MF.ConsoleApplication

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    [<RequireQualifiedAccess>]
    module Argument =
        let domain = Argument.required "domain" "Path to a file or dir containing a domain specification (in F# type notation)."
        let tuc = Argument.required "tuc" "Path to a .tuc file containing a use-case."

    type DomainArgument =
        | SingleFile of string
        | Dir of string * string list

    [<RequireQualifiedAccess>]
    module Input =
        open System.IO

        let getDomain ((input, output): IO) =
            let domain =
                match input |> Input.getArgumentValueAsString "domain" with
                | Some fsx when fsx |> File.Exists && fsx.EndsWith ".fsx" ->
                    SingleFile fsx

                | Some dir when dir |> Directory.Exists ->
                    Dir (
                        dir,
                        [ dir ] |> FileSystem.getAllFiles |> List.filter (fun f -> f.EndsWith ".fsx")
                    )

                | invalidPath -> failwithf "Path to domain file(s) %A is invalid." invalidPath

            match domain with
            | SingleFile file -> [[ file ]]
            | Dir (_, files) -> files |> List.map List.singleton
            |> output.Options "Domain file(s):"

            domain

        let getTuc ((input, output): IO) =
            match input |> Input.getArgumentValueAsString "tuc" with
            | Some tuc when tuc |> File.Exists && tuc.EndsWith ".tuc" ->
                tuc
                |> tee (List.singleton >> List.singleton >> output.Options "Tuc file:")
            | invalidPath -> failwithf "Path to tuc file %A is invalid." invalidPath

    open System.IO

    [<RequireQualifiedAccess>]
    type WatchSubdirs =
        | Yes
        | No

    let private runForever = async {
        while true do
            do! Async.Sleep 1000
    }

    let watch output watchSubdirs execute (path, filter) = async {
        let includeSubDirs =
            match watchSubdirs with
            | WatchSubdirs.Yes -> true
            | WatchSubdirs.No -> false

        let pathDir, fileName =
            if path |> Directory.Exists then path, None
            elif path |> File.Exists then Path.GetDirectoryName(path), Some path
            else failwithf "Path %A is invalid." path

        use watcher =
            new FileSystemWatcher(
                Path = pathDir,
                EnableRaisingEvents = true,
                IncludeSubdirectories = includeSubDirs
            )

        watcher.Filters.Add(filter)

        match fileName with
        | Some fileName ->
            watcher.Filters.Add(fileName)
        | _ -> ()

        if output.IsDebug() then
            sprintf "<c:gray>[Watch]</c> Path: <c:cyan>%s</c> | Filters: <c:yellow>%s</c> | With subdirs: <c:magenta>%A</c>"
                path
                (watcher.Filters |> String.concat "; ")
                includeSubDirs
            |> output.Message

        watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.LastWrite
        watcher.SynchronizingObject <- null

        let notifyWatch () =
            path
            |> sprintf "<c:gray>[Watch]</c> Watching path <c:dark-yellow>%A</c> (Press <c:yellow>ctrl + c</c> to stop) ...\n"
            |> output.Message

        let executeOnWatch event =
            if output.IsDebug() then output.Message <| sprintf "<c:gray>[Watch]</c> Source %s." event

            output.Message "<c:gray>[Watch]</c> Executing ...\n"

            try execute()
            with e -> output.Error <| sprintf "%A" e

            notifyWatch ()

        watcher.Changed.Add(fun _ -> executeOnWatch "changed")
        watcher.Created.Add(fun _ -> executeOnWatch "created")
        watcher.Deleted.Add(fun _ -> executeOnWatch "deleted")
        watcher.Renamed.Add(fun _ -> executeOnWatch "renamed")

        if output.IsVerbose() then
            output.Message <| sprintf "<c:gray>[Watch]</c> Enabled for %A" path

        notifyWatch()

        do! runForever
    }

    let executeAndWaitForWatch output execute = async {
        try execute()
        with e -> output.Error <| sprintf "%A" e

        do! runForever
    }

    open MF.Domain
    open ErrorHandling

    let parseDomain (input, output) domain =
        let domain =
            match domain with
            | Some domain -> domain
            | _ -> (input, output) |> Input.getDomain

        match domain with
        | SingleFile file -> [ file ]
        | Dir (_, files) -> files
        |> List.map (Parser.parse output)

    let checkDomain (input, output) domain =
        result {
            let parsedDomains =
                domain
                |> parseDomain (input, output)

            let! resolvedTypes =
                parsedDomains
                |> Resolver.resolve output
                |> Result.mapError UnresolvedTypes

            let! domainTypes =
                resolvedTypes
                |> Checker.check output
                |> Result.mapError UndefinedTypes

            return domainTypes
        }

    let showParseDomainError output = function
        | UnresolvedTypes unresolvedTypes ->
            unresolvedTypes
            |> List.map (TypeName.value >> List.singleton)
            |> output.Options (sprintf "Unresolved types [%d]:" (unresolvedTypes |> List.length))

            output.Error "You have to solve unresolved types first.\n"
        | UndefinedTypes undefinedTypes ->
            undefinedTypes
            |> List.map (TypeName.value >> List.singleton)
            |> output.Options (sprintf "Undefined types [%d]:" (undefinedTypes |> List.length))

            output.Error "You have to define all types first.\n"
