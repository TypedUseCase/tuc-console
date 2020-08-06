namespace MF.TucConsole

module Console =
    open MF.ConsoleApplication

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    [<RequireQualifiedAccess>]
    module Argument =
        let domain = Argument.required "domain" "Path to a file or dir containing a domain specification (in F# type notation)."

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

                | invalidPath -> failwithf "Domain path %A is invalid." invalidPath

            match domain with
            | SingleFile file -> [[ file ]]
            | Dir (_, files) -> files |> List.map List.singleton
            |> output.Options "Domain files:"

            domain

    open System.IO

    [<RequireQualifiedAccess>]
    type WatchSubdirs =
        | Yes
        | No

    let watch output watchSubdirs execute (path, filter) =
        let includeSubDirs =
            match watchSubdirs with
            | WatchSubdirs.Yes -> true
            | WatchSubdirs.No -> false

        use watcher =
            new FileSystemWatcher(
                Path = path,
                Filter = filter,
                EnableRaisingEvents = true,
                IncludeSubdirectories = includeSubDirs
            )

        watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.LastWrite
        watcher.SynchronizingObject <- null

        let notifyWatch () =
            path
            |> sprintf " <c:dark-yellow>! Watching path %A</c> (Press <c:yellow>ctrl + c</c> to stop) ...\n"
            |> output.Message

        let executeOnWatch event =
            if output.IsDebug() then output.Message <| sprintf "[Watch] Source %s." event

            output.Message "Executing ...\n"

            try execute None
            with e -> output.Error e.Message

            notifyWatch ()

        watcher.Changed.Add(fun _ -> executeOnWatch "changed")
        watcher.Created.Add(fun _ -> executeOnWatch "created")
        watcher.Deleted.Add(fun _ -> executeOnWatch "deleted")
        watcher.Renamed.Add(fun _ -> executeOnWatch "renamed")

        executeOnWatch "init"

        while true do ()

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

