namespace ProjectBuild

module internal PlantUml =
    open System
    open System.Diagnostics
    open System.IO
    open System.IO.Compression
    open System.Net.Http
    open System.Security.Cryptography
    open Fake.Core
    open Fake.Core.TargetOperators
    open Alma.Build.Utils

    type private EnsureResult =
        | Verified of version: string * rid: string
        | Downloaded of version: string * rid: string

    type private Asset = {
        Rid: string
        Url: string
        Sha256: string
        Executable: string
    }

    let private manifestPath = "tools/plantuml/manifest.txt"
    let private stagingRoot = "tools/plantuml/native"

    let private manifest =
        manifestPath
        |> File.ReadLines
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> Seq.filter (fun line -> line.StartsWith "#" |> not)
        |> Seq.map (fun line ->
            let separator = line.IndexOf '='

            if separator < 1 then
                failwithf "PlantUML manifest has malformed line %A." line

            line.Substring(0, separator), line.Substring(separator + 1)
        )
        |> Seq.groupBy fst
        |> Seq.map (fun (key, entries) ->
            let values = entries |> Seq.map snd |> Seq.toList

            match values with
            | [ value ] -> key, value
            | _ -> failwithf "PlantUML manifest has duplicate key %A." key
        )
        |> Map.ofSeq

    let private getManifestValue key =
        manifest
        |> Map.tryFind key
        |> Option.defaultWith (fun () -> failwithf "PlantUML manifest is missing %A." key)

    let private ridPrefix = "rid."
    let private sha256Suffix = ".sha256"

    let private supportedRids =
        manifest
        |> Map.toSeq
        |> Seq.choose (fun (key, _) ->
            if key.StartsWith ridPrefix && key.EndsWith sha256Suffix
            then Some (key.Substring(ridPrefix.Length, key.Length - ridPrefix.Length - sha256Suffix.Length))
            else None
        )
        |> Set.ofSeq

    type private SupportedRid = private SupportedRid of string

    module private SupportedRid =
        let create rid =
            if supportedRids |> Set.contains rid
            then SupportedRid rid
            else failwithf "PlantUML native runtime is not available for %s. Supported runtimes: %s." rid (supportedRids |> String.concat ", ")

        let value (SupportedRid rid) = rid

    let private hash path =
        use archive = File.OpenRead path
        archive |> SHA256.HashData |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

    let private asset (rid: SupportedRid) =
        let rid = SupportedRid.value rid
        let prefix = sprintf "rid.%s." rid
        let checksum = getManifestValue (prefix + "sha256")

        if checksum.Length <> 64 || checksum |> Seq.exists (Uri.IsHexDigit >> not) then
            failwithf "PlantUML manifest checksum for %s is malformed." rid

        {
            Rid = rid
            Url = getManifestValue (prefix + "url")
            Sha256 = checksum
            Executable = getManifestValue (prefix + "executable")
        }

    let private archivePath asset = Path.Combine(stagingRoot, asset.Rid, "plantuml.zip")
    let private stagingPath asset = Path.Combine(stagingRoot, asset.Rid, "runtime")
    let private executablePath asset = Path.Combine(stagingPath asset, asset.Executable)

    let private legalEvidenceIsPresent () =
        [ "license-text"; "notice"; "source-provenance" ]
        |> List.iter (getManifestValue >> File.Exists >> function true -> () | false -> failwith "PlantUML legal evidence is missing.")

    let private archiveMatches asset =
        let path = archivePath asset
        File.Exists path && hash path = asset.Sha256

    let private stagingMatches asset =
        let marker = Path.Combine(stagingPath asset, ".sha256")
        File.Exists(executablePath asset) && File.Exists(marker) && File.ReadAllText(marker) = asset.Sha256

    let private deleteDirectory path =
        if Directory.Exists path then Directory.Delete(path, true)

    let private stage asset =
        let archive = archivePath asset

        let destination = stagingPath asset
        let temporary = destination + ".tmp"
        deleteDirectory temporary
        Directory.CreateDirectory temporary |> ignore

        ZipFile.ExtractToDirectory(archive, temporary)

        if OperatingSystem.IsWindows() |> not then
            File.SetUnixFileMode(Path.Combine(temporary, asset.Executable), UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute ||| UnixFileMode.GroupRead ||| UnixFileMode.GroupExecute ||| UnixFileMode.OtherRead ||| UnixFileMode.OtherExecute)

        File.WriteAllText(Path.Combine(temporary, ".sha256"), asset.Sha256)

        deleteDirectory destination
        Directory.Move(temporary, destination)

    let private smoke asset = async {
        let startInfo = ProcessStartInfo(executablePath asset)
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.ArgumentList.Add "--help"

        use childProcess = Process.Start startInfo
        let stdoutRead = childProcess.StandardOutput.ReadToEndAsync() |> Async.AwaitTask
        let stderrRead = childProcess.StandardError.ReadToEndAsync() |> Async.AwaitTask
        let! output = [ stdoutRead; stderrRead ] |> Async.Parallel
        do! childProcess.WaitForExitAsync() |> Async.AwaitTask

        if childProcess.ExitCode <> 0 then
            failwithf "PlantUML native executable smoke test failed for %s with exit code %d. Standard output: %s Standard error: %s" asset.Rid childProcess.ExitCode output[0] output[1]
    }

    let private download (url: string) (destination: string) = async {
        use client = new HttpClient()
        use! source = client.GetStreamAsync url |> Async.AwaitTask
        use target = File.Create destination
        do! source.CopyToAsync target |> Async.AwaitTask
        do! target.FlushAsync() |> Async.AwaitTask
    }

    let private ensure (rid: SupportedRid) = async {
        legalEvidenceIsPresent ()
        let asset = asset rid
        let version = getManifestValue "version"

        if archiveMatches asset then
            if stagingMatches asset |> not then stage asset
            return Verified(version, asset.Rid)
        else
            let archive = archivePath asset
            Directory.CreateDirectory(Path.GetDirectoryName archive) |> ignore
            if File.Exists archive then File.Delete archive
            let temporary = archive + ".tmp"
            if File.Exists temporary then File.Delete temporary

            do! download asset.Url temporary
            let downloadedHash = hash temporary

            if downloadedHash <> asset.Sha256 then
                File.Delete temporary
                failwithf "Downloaded PlantUML native archive checksum mismatch. Expected %s but got %s." asset.Sha256 downloadedHash

            File.Move(temporary, archive)
            stage asset
            return Downloaded(version, asset.Rid)
    }

    let private ensureCurrent () = async {
        let rid = RuntimeTarget.detect () |> RuntimeTarget.toRuntimeIdentifier |> RuntimeIdentifier.value |> SupportedRid.create
        let! result = ensure rid
        do! asset rid |> smoke
        return result
    }

    let private traceResult = function
        | Verified(version, rid) -> Trace.tracefn "[PlantUML] Using verified v%s native runtime for %s." version rid
        | Downloaded(version, rid) -> Trace.tracefn "[PlantUML] Downloaded and verified v%s native runtime for %s." version rid

    let init (runtimeTargets: RuntimeTarget list) =
        let releaseRids =
            runtimeTargets
            |> List.map (RuntimeTarget.toRuntimeIdentifier >> RuntimeIdentifier.value >> SupportedRid.create)

        Target.create "PlantUml" (fun _ ->
            ensureCurrent () |> Async.RunSynchronously |> traceResult
        )

        Target.create "PlantUmlRelease" (fun _ ->
            releaseRids
            |> List.iter (ensure >> Async.RunSynchronously >> traceResult)
        )

        "PlantUml" ==> "Build" |> ignore
        "PlantUmlRelease" ==> "Release" |> ignore
