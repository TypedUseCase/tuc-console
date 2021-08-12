#load ".fake/build.fsx/intellisense.fsx"
open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Tools.Git

// ========================================================================================================
// === F# / Console Application fake build ======================================================== 2.0.0 =
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

let changeLog = "CHANGELOG.md"
let gitCommit = Information.getCurrentSHA1(".")
let gitBranch = Information.getBranchName(".")

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

    let createProcess exe arg dir =
        CreateProcess.fromRawCommandLine exe arg
        |> CreateProcess.withWorkingDirectory dir
        |> CreateProcess.ensureExitCode

    let run proc arg dir =
        proc arg dir
        |> Proc.run
        |> ignore

    let orFail = function
        | Error e -> raise e
        | Ok ok -> ok

    let stringToOption = function
        | null | "" -> None
        | string -> Some string

[<RequireQualifiedAccess>]
module Dotnet =
    let dotnet = createProcess "dotnet"

    let run command dir = try run dotnet command dir |> Ok with e -> Error e
    let runInRoot command = run command "."
    let runOrFail command dir = run command dir |> orFail
    let runInRootOrFail command = run command "." |> orFail

[<RequireQualifiedAccess>]
module ProjectSources =
    let release =
        !! "./*.fsproj"

    let tests =
        !! "tests/*.fsproj"

    let all =
        !! "./*.fsproj"
        ++ "src/**/*.fsproj"
        ++ "tests/*.fsproj"

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
        let release = ReleaseNotes.parse (System.IO.File.ReadAllLines changeLog |> Seq.filter ((<>) "## Unreleased"))

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

    let getProjectDetails (projectPath: string) =
        let projectName = IO.Path.GetFileNameWithoutExtension(projectPath)
        (
            projectPath,
            projectName,
            IO.Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
        )

    ProjectSources.all
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (_, _, folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
    )
)

Target.create "Build" (fun _ ->
    ProjectSources.release
    |> Seq.iter (DotNet.build id)
)

Target.create "BuildTests" (fun _ ->
    ProjectSources.tests
    |> Seq.iter (DotNet.build id)
)

Target.create "Lint" <| skipOn "no-lint" (fun _ ->
    ProjectSources.all
    ++ "./Build.fsproj"
    |> Seq.iter (fun fsproj ->
        match Dotnet.runInRoot (sprintf "fsharplint lint %s" fsproj) with
        | Ok () -> Trace.tracefn "Lint %s is Ok" fsproj
        | Error e -> raise e
    )
)

Target.create "Tests" (fun _ ->
    if ProjectSources.tests |> Seq.isEmpty
    then Trace.tracefn "There are no tests yet."
    else Dotnet.runOrFail "run" "tests"
)

let zipRelease releaseDir =
    if releaseDir </> "zipCompiled" |> File.exists
    then
        Trace.tracefn "\nZipping released files in %s ..." releaseDir
        run (createProcess "zipCompiled") "" releaseDir
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

    ProjectSources.release
    |> Seq.collect (fun project -> runtimeIds |> List.collect (fun runtimeId -> [project, runtimeId]))
    |> Seq.iter (fun (project, runtimeId) ->
        sprintf "publish -c Release /p:PublishSingleFile=true -o %s/%s --self-contained -r %s %s" releaseDir runtimeId runtimeId project
        |> Dotnet.runInRootOrFail
    )

    zipRelease releaseDir
)

Target.create "Watch" (fun _ ->
    Dotnet.runInRootOrFail "watch run"
)

Target.create "Run" (fun _ ->
    Dotnet.runInRootOrFail "run"
)

// --------------------------------------------------------------------------------------------------------
// 4. FAKE targets hierarchy
// --------------------------------------------------------------------------------------------------------

"Clean"
    ==> "AssemblyInfo"
    ==> "Build" <=> "BuildTests"
    ==> "Lint"
    ==> "Tests"
    ==> "Release"

"Build"
    ==> "Watch" <=> "Run"

Target.runOrDefaultWithArguments "Build"
