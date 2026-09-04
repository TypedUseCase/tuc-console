module Tuc.Console.Tests.RendererTests

open System
open System.Diagnostics
open System.IO
open System.Text
open Expecto
open Tuc.Console.Tests.Fixture
open Tuc.Puml

[<RequireQualifiedAccess>]
module private Fixture =
    let executable = Path.Combine(AppContext.BaseDirectory, "fixture" + if OperatingSystem.IsWindows() then ".exe" else "")

    let executablePath name =
        let path = Path.Combine(Path.GetTempPath(), sprintf "tuc-renderer-%s-%s" (Guid.NewGuid().ToString "N") name)
        File.WriteAllText(path, "")
        path

    let resultOrFail = function
        | Ok value -> value
        | Error error -> failtestf "Fixture setup should succeed: %A" error

    let command mode = {
        Executable = PlantUmlExecutable.create executable
        Arguments = FixtureMode.serialize mode
    }

    let nativeRenderer () =
        let executable = NativeRuntime.bundled () |> resultOrFail
        let settings: RendererSettings = { PlantUmlExecutable = executable }
        settings |> Renderer.create |> resultOrFail

[<Tests>]
let protocolTests =
    testList "PlantUML process protocol" [
        testCaseAsync "should return UTF-8 input bytes when fixture echoes standard input" <| async {
            let source = "@startuml\nAlice -> Bob: cafe\n@enduml\n"

            let! result = Renderer.runCommand (Fixture.command FixtureMode.Echo) source

            Expect.equal result (Ok (Encoding.UTF8.GetBytes source)) "Renderer should preserve standard output bytes"
        }

        testCaseAsync "should return RenderingFailed error when fixture exits nonzero" <| async {
            let! result = Renderer.runCommand (Fixture.command FixtureMode.Nonzero) "source"

            Expect.equal result (Error (RenderError.RenderingFailed (FixtureMode.failureExitCode, FixtureMode.failureOutput))) "Renderer should preserve exit code and standard error"
        }

        testCaseAsync "should return StartFailed error when executable cannot start" <| async {
            let path = Fixture.executablePath "not-executable"
            let command: RenderCommand = { Executable = PlantUmlExecutable.create path; Arguments = [] }

            try
                let! result = Renderer.runCommand command "source"

                match result with
                | Error (RenderError.StartFailed _) -> ()
                | _ -> failtestf "Renderer should return StartFailed, got %A" result
            finally
                File.Delete path
        }

        testCaseAsync "should drain both streams when fixture emits high output" <| async {
            let! result = Renderer.runCommand (Fixture.command FixtureMode.HighOutput) "source"

            match result with
            | Ok output -> Expect.equal output.Length FixtureMode.highOutputLength "Renderer should drain the complete standard output"
            | Error error -> failtestf "Renderer should not block, got %A" error
        }
    ]

[<Tests>]
let rendererTests =
    testList "PlantUML native renderer" [
        testCase "should return ExecutableNotFound error when executable path is absent" <| fun _ ->
            let executable = PlantUmlExecutable.create (Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString "N"))

            let result = Renderer.create { PlantUmlExecutable = executable }

            Expect.equal result (Error (RenderError.ExecutableNotFound executable)) "Renderer should validate native executable path"

        testCase "should construct pipe command when rendering SVG" <| fun _ ->
            let executable = PlantUmlExecutable.create Fixture.executable
            let renderer = Renderer.create { PlantUmlExecutable = executable } |> Fixture.resultOrFail

            let command = Renderer.plantUmlCommand renderer RenderFormat.Svg

            Expect.equal (PlantUmlExecutable.value command.Executable) Fixture.executable "Command should start native executable"
            Expect.equal command.Arguments [ "-tsvg"; "-pipe"; "-charset"; "UTF-8" ] "Command should use native pipe protocol"

        testCaseAsync "should render SVG when bundled native runtime is extracted" <| async {
            let renderer = Fixture.nativeRenderer ()

            let! result = Renderer.render renderer RenderFormat.Svg "@startuml\nAlice -> Bob: native SVG\n@enduml"

            match result with
            | Ok image -> image |> Encoding.UTF8.GetString |> fun svg -> Expect.stringContains svg "native SVG" "Native renderer should generate SVG"
            | Error error -> failtestf "Native SVG rendering should succeed, got %A" error
        }

        testCaseAsync "should render PNG when bundled native runtime is extracted" <| async {
            let renderer = Fixture.nativeRenderer ()

            let! result = Renderer.render renderer RenderFormat.Png "@startuml\nAlice -> Bob: native PNG\n@enduml"

            match result with
            | Ok image -> Expect.sequenceStarts image [| 137uy; 80uy; 78uy; 71uy |] "Native renderer should generate PNG signature"
            | Error error -> failtestf "Native PNG rendering should succeed, got %A" error
        }

        testCaseAsync "should complete concurrent renders when one extracted runtime is shared" <| async {
            let renderer = Fixture.nativeRenderer ()
            let source = "@startuml\nAlice -> Bob: concurrent\n@enduml"

            let! results = [ 1..10 ] |> List.map (fun _ -> Renderer.render renderer RenderFormat.Svg source) |> Async.Parallel

            results
            |> Array.iter (function
                | Ok _ -> ()
                | Error error -> failtestf "Concurrent native render should succeed, got %A" error
            )
        }
    ]

[<Tests>]
let tests = testList "PlantUML renderer" [ protocolTests; rendererTests ]
