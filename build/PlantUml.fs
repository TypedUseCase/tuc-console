namespace ProjectBuild

module internal PlantUml =
    open System
    open System.IO
    open System.Net.Http
    open System.Security.Cryptography

    type EnsureResult =
        | Verified of version: string
        | Downloaded of version: string

    let private manifestPath = "tools/plantuml/manifest.txt"
    let private jarPath = "tools/plantuml/plantuml.jar"

    let private manifest =
        manifestPath
        |> File.ReadLines
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> Seq.map (fun line ->
            let separator = line.IndexOf '='
            line.Substring(0, separator), line.Substring(separator + 1)
        )
        |> Map.ofSeq

    let private getManifestValue key =
        manifest
        |> Map.tryFind key
        |> Option.defaultWith (fun () -> failwithf "PlantUML manifest is missing %A." key)

    let private hash path =
        path
        |> File.ReadAllBytes
        |> SHA256.HashData
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let private isVerified path =
        File.Exists path && hash path = getManifestValue "sha256"

    let ensure () =
        let version = getManifestValue "version"

        if isVerified jarPath then Verified version
        else
            if File.Exists jarPath then File.Delete jarPath

            let url = getManifestValue "url"
            let directory = Path.GetDirectoryName jarPath
            Directory.CreateDirectory directory |> ignore

            use client = new HttpClient()
            let jar = client.GetByteArrayAsync url |> Async.AwaitTask |> Async.RunSynchronously
            let downloadedHash = jar |> SHA256.HashData |> Convert.ToHexString |> fun value -> value.ToLowerInvariant()

            if downloadedHash <> getManifestValue "sha256" then
                failwithf "Downloaded PlantUML JAR checksum mismatch. Expected %s but got %s." (getManifestValue "sha256") downloadedHash

            File.WriteAllBytes(jarPath, jar)
            Downloaded version
