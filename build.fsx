#load ".fake/build.fsx/intellisense.fsx"
open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Tools.Git

type ToolDir =
    /// Global tool dir must be in PATH - ${PATH}:/root/.dotnet/tools
    | Global
    /// Just a dir name, the location will be used as: ./{LocalDirName}
    | Local of string

// ========================================================================================================
// === F# / Console Application fake build ======================================================== 1.3.0 =
// --------------------------------------------------------------------------------------------------------
// Options:
//  - no-clean   - disables clean of dirs in the first step (required on CI)
//  - no-lint    - lint will be executed, but the result is not validated
// --------------------------------------------------------------------------------------------------------
// Table of contents:
//      1. Information about project, configuration
//      2. Utilities, DotnetCore functions
//      3. FAKE targets
//      4. FAKE targets hierarchy
// ========================================================================================================

// --------------------------------------------------------------------------------------------------------
// 1. Information about the project to be used at NuGet and in AssemblyInfo files and other FAKE configuration
// --------------------------------------------------------------------------------------------------------

let project = "TUC Console"
let summary = "Console application for .tuc commands."

let release = ReleaseNotes.parse (System.IO.File.ReadAllLines "CHANGELOG.md" |> Seq.filter ((<>) "## Unreleased"))
let gitCommit = Information.getCurrentSHA1(".")
let gitBranch = Information.getBranchName(".")

let toolsDir = Global

/// Runtime IDs: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog#macos-rids
let runtimeIds =
    [
        "osx-x64"
        "win-x64"
        "linux-x64"
    ]

// --------------------------------------------------------------------------------------------------------
// 2. Utilities, DotnetCore functions, etc.
// --------------------------------------------------------------------------------------------------------

[<AutoOpen>]
module private Utils =
    let tee f a =
        f a
        a

    let skipOn option action p =
        if p.Context.Arguments |> Seq.contains option
        then Trace.tracefn "Skipped ..."
        else action p

module private DotnetCore =
    let run cmd workingDir =
        let options =
            DotNet.Options.withWorkingDirectory workingDir
            >> DotNet.Options.withRedirectOutput true

        DotNet.exec options cmd ""

    let runOrFail cmd workingDir =
        run cmd workingDir
        |> tee (fun result ->
            if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir
        )
        |> ignore

    let runInRoot cmd = run cmd "."
    let runInRootOrFail cmd = runOrFail cmd "."

    let installOrUpdateTool toolDir tool =
        let toolCommand action =
            match toolDir with
            | Global -> sprintf "tool %s --global %s" action tool
            | Local dir -> sprintf "tool %s --tool-path ./%s %s" action dir tool

        match runInRoot (toolCommand "install") with
        | { ExitCode = code } when code <> 0 ->
            match runInRoot (toolCommand "update") with
            | { ExitCode = code } when code <> 0 -> Trace.tracefn "Warning: Install and update of %A has failed." tool
            | _ -> ()
        | _ -> ()

    let execute command args (dir: string) =
        let cmd =
            sprintf "%s/%s"
                (dir.TrimEnd('/'))
                command

        let processInfo = System.Diagnostics.ProcessStartInfo(cmd)
        processInfo.RedirectStandardOutput <- true
        processInfo.RedirectStandardError <- true
        processInfo.UseShellExecute <- false
        processInfo.CreateNoWindow <- true
        processInfo.Arguments <- args |> String.concat " "

        use proc =
            new System.Diagnostics.Process(
                StartInfo = processInfo
            )
        if proc.Start() |> not then failwith "Process was not started."
        proc.WaitForExit()

        if proc.ExitCode <> 0 then failwithf "Command '%s' failed in %s." command dir
        (proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd())

let stringToOption = function
    | null | "" -> None
    | string -> Some string

// --------------------------------------------------------------------------------------------------------
// 3. Targets for FAKE
// --------------------------------------------------------------------------------------------------------

Target.create "Clean" <| skipOn "no-clean" (fun _ ->
    !! "./**/bin/Release"
    ++ "./**/bin/Debug"
    ++ "./**/obj"
    ++ "./**/.ionide"
    |> Shell.cleanDirs
)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        let now = DateTime.Now

        let gitValue initialValue =
            initialValue
            |> stringToOption
            |> Option.defaultValue "unknown"

        [
            AssemblyInfo.Title projectName
            AssemblyInfo.Product project
            AssemblyInfo.Description summary
            AssemblyInfo.Version release.AssemblyVersion
            AssemblyInfo.FileVersion release.AssemblyVersion
            AssemblyInfo.InternalsVisibleTo "tests"
            AssemblyInfo.Metadata("gitbranch", gitBranch |> gitValue)
            AssemblyInfo.Metadata("gitcommit", gitCommit |> gitValue)
            AssemblyInfo.Metadata("createdAt", now.ToString("yyyy-MM-dd HH:mm:ss"))
        ]

    let getProjectDetails projectPath =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)
        (
            projectPath,
            projectName,
            IO.Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
        )

    !! "**/*.fsproj"
    -- "example/**/*.*proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (_, _, folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
    )
)

Target.create "Build" (fun _ ->
    !! "**/*.fsproj"
    -- "example/**/*.*proj"
    |> Seq.iter (DotNet.build id)
)

Target.create "Lint" <| skipOn "no-lint" (fun _ ->
    DotnetCore.installOrUpdateTool toolsDir "dotnet-fsharplint"

    let checkResult (messages: string list) =
        let rec check: string list -> unit = function
            | [] -> failwithf "Lint does not yield a summary."
            | head :: rest ->
                if head.Contains "Summary" then
                    match head.Replace("= ", "").Replace(" =", "").Replace("=", "").Replace("Summary: ", "") with
                    | "0 warnings" -> Trace.tracefn "Lint: OK"
                    | warnings -> failwithf "Lint ends up with %s." warnings
                else check rest
        messages
        |> List.rev
        |> check

    !! "**/*.*proj"
    -- "example/**/*.*proj"
    |> Seq.map (fun fsproj ->
        match toolsDir with
        | Global ->
            DotnetCore.runInRoot (sprintf "fsharplint lint %s" fsproj)
            |> fun (result: ProcessResult) -> result.Messages
        | Local dir ->
            DotnetCore.execute "dotnet-fsharplint" ["lint"; fsproj] dir
            |> fst
            |> tee (Trace.tracefn "%s")
            |> String.split '\n'
            |> Seq.toList
    )
    |> Seq.iter checkResult
)

Target.create "Tests" (fun _ ->
    if !! "tests/*.fsproj" |> Seq.isEmpty
    then Trace.tracefn "There are no tests yet."
    else DotnetCore.runOrFail "run" "tests"
)

let zipRelease releaseDir =
    if releaseDir </> "zipCompiled" |> File.exists
    then
        Trace.tracefn "\nZipping released files in %s ..." releaseDir
        releaseDir
        |> DotnetCore.execute "zipCompiled" []
        |> Trace.tracefn "Zip result:\n%A\n"
    else
        Trace.tracefn "\nZip compiled files"
        runtimeIds
        |> List.iter (fun runtimeId ->
            Trace.tracefn " -> zipping %s ..." runtimeId
            let zipFile = sprintf "%s.zip" runtimeId
            IO.File.Delete zipFile
            Zip.zip releaseDir (releaseDir </> zipFile) !!(releaseDir </> runtimeId </> "*")
        )

Target.create "Release" (fun _ ->
    let releaseDir = Path.getFullName "./dist"

    !! "**/*.*proj"
    -- "example/**/*.*proj"
    -- "tests/**/*.*proj"
    |> Seq.collect (fun project -> runtimeIds |> List.collect (fun runtimeId -> [project, runtimeId]))
    |> Seq.iter (fun (project, runtimeId) ->
        sprintf "publish -c Release /p:PublishSingleFile=true -o %s/%s --self-contained -r %s %s" releaseDir runtimeId runtimeId project
        |> DotnetCore.runInRootOrFail
    )

    zipRelease releaseDir
)

Target.create "Watch" (fun _ ->
    DotnetCore.runInRootOrFail "watch run"
)

Target.create "Run" (fun _ ->
    DotnetCore.runInRootOrFail "run"
)

// --------------------------------------------------------------------------------------------------------
// 4. FAKE targets hierarchy
// --------------------------------------------------------------------------------------------------------

"Clean"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "Lint"
    ==> "Tests"
    ==> "Release"

"Build"
    ==> "Watch" <=> "Run"

Target.runOrDefaultWithArguments "Build"
