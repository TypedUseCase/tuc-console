namespace Tuc.Console

module Console =
    open System.IO
    open MF.ConsoleApplication

    [<RequireQualifiedAccess>]
    module Argument =
        let domain = Argument.required "domain" "Path to a file or dir containing a domain specification (in F# type notation)."
        let tucFile = Argument.required "tuc" "Path to a .tuc file containing a use-case."
        let tucFileOrDir = Argument.required "tuc" "Path to a .tuc file or a dir containing .tuc files."

    [<RequireQualifiedAccess>]
    type WatchSubdirs =
        | Yes
        | No

    type FileOrDir =
        | File of string
        | Dir of string * string list

    [<RequireQualifiedAccess>]
    module FileOrDir =
        let parse (extension: string) = function
            | Some file when file |> File.Exists && file.EndsWith extension ->
                FileOrDir.File file

            | Some dir when dir |> Directory.Exists ->
                Dir (
                    dir,
                    [ dir ] |> FileSystem.getAllFiles |> List.filter (fun f -> f.EndsWith extension)
                )

            | invalidPath -> failwithf "Path to file(s) %A is invalid." invalidPath

        let debug (output: Output) title = output.Options (sprintf "%s file(s):" title) << function
            | File file -> [[ file ]]
            | Dir (_, files) -> files |> List.map List.singleton

        let file = function
            | File file -> Some file
            | _ -> None

        let files = function
            | File file -> [ file ]
            | Dir (_, files) -> files

        let watch = function
            | File file -> file, WatchSubdirs.No
            | Dir (dir, _) -> dir, WatchSubdirs.Yes

    [<RequireQualifiedAccess>]
    module Input =
        let getDomain ((input, output): IO) =
            input
            |> Input.Argument.asString "domain"
            |> FileOrDir.parse "Domain.fsx"
            |> tee (FileOrDir.debug output "Domain")

        let getTuc ((input, output): IO) =
            let path = input |> Input.Argument.asString "tuc"

            path
            |> FileOrDir.parse ".tuc"
            |> FileOrDir.file
            |> function
                | Some file -> file
                | _ -> failwithf "Path to tuc file %A is invalid." path
            |> tee (FileOrDir.File >> FileOrDir.debug output "Tuc")

        let getTucFileOrDir ((input, output): IO) =
            input
            |> Input.Argument.asString "tuc"
            |> FileOrDir.parse ".tuc"
            |> tee (FileOrDir.debug output "Tuc")

        let getStyle ((input, output): IO) =
            match input with
            | Input.Option.Has "style" styleFile ->
                let styleFile = styleFile |> OptionValue.value "style"

                if styleFile |> File.Exists |> not
                    then failwithf "Style file does not exist at path %s" styleFile

                Some (styleFile |> File.ReadAllText)
            | _ -> None

    [<RequireQualifiedAccess>]
    module internal Watch =
        let private runForever = async {
            while true do
                do! Async.Sleep 1000
        }

        let watch (output: Output) watchSubdirs execute (path, filter) = async {
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
                    // fsharplint:disable-next-line
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

                try execute |> Async.RunSynchronously
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

        let executeAndWaitForWatch (output: Output) execute = async {
            try do! execute
            with e -> output.Error <| sprintf "%A" e

            do! runForever
        }

    open Tuc.Domain
    open MF.ErrorHandling

    let parseDomain (input, output) domain =
        match domain with
        | Some domain -> domain
        | _ -> (input, output) |> Input.getDomain
        |> FileOrDir.files
        |> List.map (Parser.parse output)
        |> AsyncResult.handleAsyncResults output ParseError

    [<RequireQualifiedAccess>]
    type CheckDomainError =
        | ParseErrors of ParseError list
        | ResolveError of ResolveError
        | ParseDomainError of ParseDomainError

    let checkDomain (input, output) domain: AsyncResult<DomainType list, CheckDomainError> =
        asyncResult {
            let! parsedDomains =
                domain
                |> parseDomain (input, output)
                |> AsyncResult.mapError CheckDomainError.ParseErrors

            let! resolvedTypes =
                parsedDomains
                |> Resolver.resolve output
                |> Result.mapError CheckDomainError.ResolveError

            let! domainTypes =
                resolvedTypes
                |> Checker.check output
                |> Result.mapError (UndefinedTypes >> CheckDomainError.ParseDomainError)

            return domainTypes
        }

    module CheckDomainError =
        let show (output: Output) = function
            | CheckDomainError.ParseErrors errors ->
                errors
                |> List.map (ParseError.format >> List.singleton)
                |> List.distinct
                |> output.Options "Parse errors:"

                output.Error "You have to fix all parse errors first.\n"

            | CheckDomainError.ResolveError (ResolveError.UnresolvedTypes unresolvedTypes) ->
                unresolvedTypes
                |> List.map (TypeName.value >> List.singleton)
                |> output.Options (sprintf "Unresolved types [%d]:" (unresolvedTypes |> List.length))

                output.Error "You have to solve unresolved types first.\n"

            | CheckDomainError.ParseDomainError (UnresolvedTypes unresolvedTypes) ->
                unresolvedTypes
                |> List.map (TypeName.value >> List.singleton)
                |> output.Options (sprintf "Unresolved types [%d]:" (unresolvedTypes |> List.length))

                output.Error "You have to solve unresolved types first.\n"

            | CheckDomainError.ParseDomainError (UndefinedTypes undefinedTypes) ->
                undefinedTypes
                |> List.map (TypeName.value >> List.singleton)
                |> output.Options (sprintf "Undefined types [%d]:" (undefinedTypes |> List.length))

                output.Error "You have to define all types first.\n"
