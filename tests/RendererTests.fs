module Tuc.Console.Tests.RendererTests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open Expecto
open Tuc.Console.Tests.Fixture
open Tuc.Puml

[<RequireQualifiedAccess>]
module private BoundaryFixture =
    let path name = Path.Combine(Path.GetTempPath(), sprintf "tuc-renderer-%s-%s" (Guid.NewGuid().ToString "N") name)

    let resultOrFail = function
        | Ok value -> value
        | Error error -> failtestf "Fixture setup should succeed: %A" error

    let file name =
        let path = path name
        File.WriteAllText(path, "")
        path

[<RequireQualifiedAccess>]
module private RendererProcessFixture =
    let executable = Path.Combine(AppContext.BaseDirectory, "fixture" + if OperatingSystem.IsWindows() then ".exe" else "")
    let source = "source"
    let private fileWaitAttempts = 100

    let createRenderCommand (mode: FixtureMode) = {
        Executable = JavaExecutable.create executable |> BoundaryFixture.resultOrFail
        Arguments = mode |> FixtureMode.serialize
    }

    let start (mode: FixtureMode) =
        let startInfo = ProcessStartInfo(executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
        mode |> FixtureMode.serialize |> List.iter startInfo.ArgumentList.Add

        Process.Start startInfo

    let run (mode: FixtureMode) = async {
        use childProcess = start mode
        let stdout = childProcess.StandardOutput.ReadToEndAsync()
        let stderr = childProcess.StandardError.ReadToEndAsync()
        do! childProcess.WaitForExitAsync() |> Async.AwaitTask
        let! stdout = stdout |> Async.AwaitTask
        let! stderr = stderr |> Async.AwaitTask

        return childProcess.ExitCode, stdout, stderr
    }

    [<TailCall>]
    let rec private waitForFileWithAttempts attempts path = async {
        if File.Exists path then
            return ()
        elif attempts = 0 then
            failtestf "Fixture did not write its process record: %s" path
        else
            do! Async.Sleep 10
            return! waitForFileWithAttempts (attempts - 1) path
    }

    let waitForFile path =
        waitForFileWithAttempts fileWaitAttempts path

    let isAlive processId =
        try
            use childProcess = Process.GetProcessById processId
            not childProcess.HasExited
        with :? ArgumentException ->
            false

[<RequireQualifiedAccess>]
module private LocalPlantUml =
    let jarPath = Path.Combine(AppContext.BaseDirectory, "plantuml", "plantuml.jar")

    let createRenderer jarPath =
        { PlantUmlJar = PlantUmlJar.create jarPath }
        |> Renderer.create
        |> BoundaryFixture.resultOrFail

// Test that the process fixture behaves correctly under all configurations
// so the tests using it can rely on its behavior
[<Tests>]
let processFixtureTests =
    testList "PlantUML process fixture" [
        testCaseAsync "should emit success output when invoked with a success configuration" <| async {
            let! exitCode, stdout, stderr = RendererProcessFixture.run FixtureMode.Success

            Expect.equal exitCode FixtureMode.successExitCode "Successful fixture should exit successfully"
            Expect.equal stdout FixtureMode.successOutput "Successful fixture should emit deterministic output"
            Expect.equal stderr "" "Successful fixture should not emit errors"
        }

        testCaseAsync "should emit stderr and a nonzero exit code when configured to fail" <| async {
            let! exitCode, stdout, stderr = RendererProcessFixture.run FixtureMode.Nonzero

            Expect.equal exitCode FixtureMode.failureExitCode "Failing fixture should expose its configured exit code"
            Expect.equal stdout "" "Failing fixture should not emit standard output"
            Expect.equal stderr FixtureMode.failureOutput "Failing fixture should emit deterministic errors"
        }

        testCaseAsync "should emit large standard output and standard error without blocking" <| async {
            let! exitCode, stdout, stderr = RendererProcessFixture.run FixtureMode.HighOutput

            Expect.equal exitCode 0 "High-output fixture should exit successfully"
            Expect.equal stdout.Length FixtureMode.highOutputLength "High-output fixture should emit one mebibyte to standard output"
            Expect.equal stderr.Length FixtureMode.highOutputLength "High-output fixture should emit one mebibyte to standard error"
        }

        testCaseAsync "should terminate its process tree when the fixture parent is killed" <| async {
            let processRecordPath = BoundaryFixture.path "processes"
            let fixtureProcess = RendererProcessFixture.start (FixtureMode.Cancellable processRecordPath)

            try
                do! RendererProcessFixture.waitForFile processRecordPath
                let processIds = File.ReadAllLines(processRecordPath) |> Array.map Int32.Parse

                fixtureProcess.Kill true
                do! fixtureProcess.WaitForExitAsync() |> Async.AwaitTask
                do! Async.Sleep 10

                processIds
                |> Array.iter (fun processId ->
                    Expect.isFalse (RendererProcessFixture.isAlive processId) (sprintf "Fixture process %d should be terminated" processId)
                )
            finally
                fixtureProcess.Dispose()
                File.Delete processRecordPath
        }
    ]

[<Tests>]
let processProtocolTests =
    testList "PlantUML process protocol" [
        testCaseAsync "should return exact UTF-8 source bytes when the fixture echoes standard input" <| async {
            let source = "@startuml\nAlice -> Bob: caf\u00e9\n@enduml\n"

            let! result = Renderer.runCommand (RendererProcessFixture.createRenderCommand FixtureMode.Echo) source

            Expect.equal result (Ok (Encoding.UTF8.GetBytes source)) "Renderer should return the fixture's exact standard output bytes"
        }

        testCaseAsync "should return the exit code and standard error when rendering fails" <| async {
            let! result = Renderer.runCommand (RendererProcessFixture.createRenderCommand FixtureMode.Nonzero) RendererProcessFixture.source

            Expect.equal result (Error (RenderError.RenderingFailed (FixtureMode.failureExitCode, FixtureMode.failureOutput))) "Renderer should return typed process failures"
        }

        testCaseAsync "should return StartFailed when Java cannot be started" <| async {
            let javaPath = BoundaryFixture.file "not-an-executable"
            let command: RenderCommand = { Executable = JavaExecutable.create javaPath |> BoundaryFixture.resultOrFail; Arguments = [] }

            try
                let! result = Renderer.runCommand command RendererProcessFixture.source

                match result with
                | Error (RenderError.StartFailed _) -> ()
                | _ -> failtestf "Renderer should return StartFailed, got %A" result
            finally
                File.Delete javaPath
        }

        testCaseAsync "should complete when fixture emits large standard output and standard error" <| async {
            let! result = Renderer.runCommand (RendererProcessFixture.createRenderCommand FixtureMode.HighOutput) RendererProcessFixture.source

            match result with
            | Ok output -> Expect.equal output.Length FixtureMode.highOutputLength "Renderer should drain the fixture's large standard output"
            | Error error -> failtestf "Renderer should not block on high output: %A" error
        }
    ]

[<Tests>]
let cancellationTests =
    testList "PlantUML cancellation" [
        testCaseAsync "should kill the fixture process tree and return Cancelled" <| async {
            let processRecordPath = BoundaryFixture.path "renderer-processes"
            let command = RendererProcessFixture.createRenderCommand (FixtureMode.Cancellable processRecordPath)
            use cancellation = new CancellationTokenSource()
            let rendering = Async.StartAsTask(Renderer.runCommand command RendererProcessFixture.source, cancellationToken = cancellation.Token)

            try
                do! RendererProcessFixture.waitForFile processRecordPath
                cancellation.Cancel()
                let! result = rendering |> Async.AwaitTask
                let processIds = File.ReadAllLines(processRecordPath) |> Array.map Int32.Parse

                Expect.equal result (Error RenderError.Cancelled) "Cancellation should be typed"
                processIds
                |> Array.iter (fun processId ->
                    Expect.isFalse (RendererProcessFixture.isAlive processId) (sprintf "Renderer fixture process %d should be terminated" processId)
                )
            finally
                if File.Exists processRecordPath then
                    File.ReadAllLines(processRecordPath)
                    |> Array.map Int32.Parse
                    |> Array.iter (fun processId ->
                        try
                            use fixtureProcess = Process.GetProcessById processId

                            if not fixtureProcess.HasExited then
                                fixtureProcess.Kill true
                        with :? ArgumentException ->
                            ()
                    )

                File.Delete processRecordPath
        }
    ]

[<Tests>]
let adapterTests =
    testList "Generate.image adapter" [
        testCaseAsync "should return SVG bytes through the application image adapter" <| async {
            let! result = Generate.image Generate.ImageFormat.Svg (Puml "@startuml\nAlice -> Bob: adapter\n@enduml")

            match result with
            | Ok image ->
                image
                |> PumlImage.value
                |> Encoding.UTF8.GetString
                |> fun svg -> Expect.stringContains svg "adapter" "Adapter should preserve rendered SVG output"
            | Error error -> failtestf "Generate.image should render through the local adapter: %s" error
        }
    ]

[<Tests>]
let localIntegrationTests =
    testList "PlantUML local integration" [
        testCaseAsync "should render with explicit settings using the bundled JAR" <| async {
            let renderer = LocalPlantUml.createRenderer LocalPlantUml.jarPath

            let! result = Renderer.render renderer RenderFormat.Svg "@startuml\nAlice -> Bob: explicit\n@enduml"

            match result with
            | Ok image ->
                image
                |> Encoding.UTF8.GetString
                |> fun svg -> Expect.stringContains svg "explicit" "Explicit renderer settings should render SVG"
            | Error error -> failtestf "Explicit renderer settings should render: %A" error
        }

        testCaseAsync "should render when the JAR path contains spaces" <| async {
            let directory = BoundaryFixture.path "jar directory"
            Directory.CreateDirectory directory |> ignore
            let jarPath = Path.Combine(directory, "plantuml jar.jar")
            File.Copy(LocalPlantUml.jarPath, jarPath)
            let renderer = LocalPlantUml.createRenderer jarPath

            try
                let! result = Renderer.render renderer RenderFormat.Svg "@startuml\nAlice -> Bob: spaces\n@enduml"

                match result with
                | Ok image ->
                    image
                    |> Encoding.UTF8.GetString
                    |> fun svg -> Expect.stringContains svg "spaces" "ArgumentList should preserve spaced JAR paths"
                | Error error -> failtestf "Renderer should support spaced JAR paths: %A" error
            finally
                Directory.Delete(directory, true)
        }

        testCaseAsync "should complete ten concurrent renders with explicit settings" <| async {
            let renderer = LocalPlantUml.createRenderer LocalPlantUml.jarPath
            let source = "@startuml\nAlice -> Bob: concurrent\n@enduml"

            let! results =
                [ 1..10 ]
                |> List.map (fun _ -> Renderer.render renderer RenderFormat.Svg source)
                |> Async.Parallel

            results
            |> Array.iter (function
                | Ok _ -> ()
                | Error error -> failtestf "Concurrent explicit renders should succeed: %A" error
            )
        }
    ]

[<Tests>]
let tests =
    testList "PlantUML renderer" [
        testList "boundaries" [
            testCase "should prefer JAVA_HOME candidate when both candidates exist" <| fun _ ->
                let javaHome = BoundaryFixture.path "java-home"
                let javaHomeBin = Path.Combine(javaHome, "bin")
                Directory.CreateDirectory(javaHomeBin) |> ignore

                let javaHomeCandidate = Path.Combine(javaHomeBin, "java")
                let pathCandidate = BoundaryFixture.file "java-path"
                File.WriteAllText(javaHomeCandidate, "")

                try
                    let result = Renderer.discoverJavaWith "java" { JavaHome = Some javaHome; Path = Some (Path.GetDirectoryName pathCandidate) }

                    Expect.equal result (Ok (JavaExecutable.create javaHomeCandidate |> BoundaryFixture.resultOrFail)) "JAVA_HOME candidate should win"
                finally
                    Directory.Delete(javaHome, true)
                    File.Delete(pathCandidate)

            testCase "should fall back to first PATH candidate when JAVA_HOME candidate is missing" <| fun _ ->
                let pathDirectory = BoundaryFixture.path "path"
                Directory.CreateDirectory(pathDirectory) |> ignore
                let pathCandidate = Path.Combine(pathDirectory, "java")
                File.WriteAllText(pathCandidate, "")

                try
                    let result = Renderer.discoverJavaWith "java" { JavaHome = Some (BoundaryFixture.path "missing-java-home"); Path = Some pathDirectory }

                    Expect.equal result (Ok (JavaExecutable.create pathCandidate |> BoundaryFixture.resultOrFail)) "PATH candidate should be used"
                finally
                    Directory.Delete(pathDirectory, true)

            testCase "should return JavaNotFound when no candidate exists" <| fun _ ->
                let result = Renderer.discoverJavaWith "java" { JavaHome = None; Path = Some (BoundaryFixture.path "missing-path") }

                Expect.equal result (Error RenderError.JavaNotFound) "Missing candidates should return a typed error"

            testCase "should discover Java when constructing a renderer" <| fun _ ->
                let jarPath = BoundaryFixture.file "plantuml.jar"
                let javaPath = BoundaryFixture.file "java"
                let settings: RendererSettings = { PlantUmlJar = PlantUmlJar.create jarPath }
                let java = JavaExecutable.create javaPath |> BoundaryFixture.resultOrFail

                try
                    let result = Renderer.createWith (fun () -> Ok java) settings

                    Expect.isOk result "Renderer construction should discover Java"
                finally
                    File.Delete jarPath
                    File.Delete javaPath

            testCase "should return JarNotFound error when the configured JAR is absent" <| fun _ ->
                let jarPath = BoundaryFixture.path "missing-plantuml.jar"
                let javaPath = BoundaryFixture.file "java"
                let settings: RendererSettings = { PlantUmlJar = PlantUmlJar.create jarPath }
                let java = JavaExecutable.create javaPath |> BoundaryFixture.resultOrFail

                try
                    let result = Renderer.createWith (fun () -> Ok java) settings

                    Expect.equal result (Error (RenderError.JarNotFound (PlantUmlJar.create jarPath))) "Renderer construction should validate the configured JAR"
                finally
                    File.Delete javaPath

            testCase "should construct the official PlantUML pipe command" <| fun _ ->
                let jarPath = BoundaryFixture.file "plantuml.jar"
                let javaPath = BoundaryFixture.file "java"
                let settings: RendererSettings = { PlantUmlJar = PlantUmlJar.create jarPath }

                try
                    let renderer = Renderer.createWith (fun () -> JavaExecutable.create javaPath) settings |> BoundaryFixture.resultOrFail
                    let command = Renderer.plantUmlCommand renderer RenderFormat.Svg

                    Expect.equal (JavaExecutable.value command.Executable) javaPath "Command should run the resolved Java"
                    Expect.equal command.Arguments [ "-Dfile.encoding=UTF-8"; "-jar"; jarPath; "-tsvg"; "-pipe"; "-charset"; "UTF-8" ] "Command should follow the official pipe protocol"
                finally
                    File.Delete jarPath
                    File.Delete javaPath

            testCase "should map each format to its PlantUML command switch" <| fun _ ->
                let switches =
                    [
                        RenderFormat.Png, "-tpng"
                        RenderFormat.Svg, "-tsvg"
                        RenderFormat.Eps, "-teps"
                        RenderFormat.Pdf, "-tpdf"
                        RenderFormat.Vdx, "-tvdx"
                        RenderFormat.Xmi, "-txmi"
                        RenderFormat.Scxml, "-tscxml"
                        RenderFormat.Html, "-thtml"
                        RenderFormat.Ascii, "-ttxt"
                        RenderFormat.AsciiUnicode, "-tutxt"
                        RenderFormat.LaTeX, "-tlatex"
                    ]

                switches
                |> List.iter (fun (format, expected) ->
                    Expect.equal (RenderFormat.toCliSwitch format) expected (sprintf "%A should have its official switch" format)
                )

            testCase "should format each renderer error stably" <| fun _ ->
                let jarPath = BoundaryFixture.file "missing.jar"
                let jar = PlantUmlJar.create jarPath

                try
                    [
                        RenderError.JavaNotFound, "PlantUML local rendering requires Java on PATH or JAVA_HOME."
                        RenderError.JarNotFound jar, sprintf "PlantUML JAR is missing at %s." jarPath
                        RenderError.StartFailed "Cannot start Java.", "Cannot start Java."
                        RenderError.RenderingFailed (2, "PlantUML error."), "PlantUML exited with code 2: PlantUML error."
                        RenderError.CommunicationFailed "Pipe failed.", "Pipe failed."
                        RenderError.Cancelled, "PlantUML rendering was cancelled."
                    ]
                    |> List.iter (fun (error, expected) ->
                        Expect.equal (RenderError.format error) expected (sprintf "%A should format stably" error)
                    )
                finally
                    File.Delete jarPath
        ]

        testCaseAsync "should render included local file when PlantUML is bundled" <| async {
            let includedFilePath = Path.Combine(Path.GetTempPath(), sprintf "tuc-plantuml-%s.iuml" (Guid.NewGuid().ToString "N"))

            File.WriteAllText(includedFilePath, "title Local renderer fixture")

            try
                let diagram = Puml (sprintf "@startuml\n!include %s\nAlice -> Bob: hello\n@enduml" includedFilePath)
                let! result = Generate.image Generate.ImageFormat.Svg diagram

                match result with
                | Ok image ->
                    let svg = image |> PumlImage.value |> Encoding.UTF8.GetString
                    Expect.stringContains svg "Local renderer fixture" "SVG should render the local include"
                | Error error ->
                    failtestf "Local PlantUML rendering should succeed: %s" error
            finally
                File.Delete includedFilePath
        }

        testCaseAsync "should identify bundled PlantUML version when rendering SVG" <| async {
            let manifestPath = Path.Combine(AppContext.BaseDirectory, "plantuml", "manifest.txt")
            let version =
                File.ReadLines manifestPath
                |> Seq.tryPick (fun line ->
                    if line.StartsWith "version=" then Some(line.Substring "version=".Length) else None
                )
                |> Option.defaultWith (fun () -> failtestf "Bundled PlantUML manifest does not contain a version: %s" manifestPath)
            let diagram = Puml "@startuml\nAlice -> Bob: hello\n@enduml"

            let! result = Generate.image Generate.ImageFormat.Svg diagram

            match result with
            | Ok image ->
                let svg = image |> PumlImage.value |> Encoding.UTF8.GetString
                Expect.stringContains svg (sprintf "<?plantuml %s?>" version) "SVG should identify the bundled PlantUML version"
            | Error error ->
                failtestf "Bundled PlantUML rendering should succeed: %s" error
        }

    ]
