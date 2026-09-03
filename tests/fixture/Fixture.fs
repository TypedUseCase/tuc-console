namespace Tuc.Console.Tests.Fixture

open System
open System.Diagnostics
open System.IO
open System.Threading

/// Child-process behaviour requested by a test, passed as the fixture's command-line arguments.
[<RequireQualifiedAccess>]
type FixtureMode =
    | Success
    | Nonzero
    | HighOutput
    | Echo
    | Cancellable of processRecordPath: string

[<RequireQualifiedAccess>]
module FixtureMode =
    let successExitCode = 0
    let failureExitCode = 7
    let successOutput = "fixture success"
    let failureOutput = "fixture failure"

    /// Bytes written to each output stream in high-output mode; large enough to overflow an undrained pipe buffer.
    let highOutputLength = 1024 * 1024

    let serialize = function
        | FixtureMode.Success -> [ "success" ]
        | FixtureMode.Nonzero -> [ "nonzero" ]
        | FixtureMode.HighOutput -> [ "high-output" ]
        | FixtureMode.Echo -> [ "echo" ]
        | FixtureMode.Cancellable processRecordPath -> [ "cancellable"; processRecordPath ]

    let parse = function
        | [ "success" ] -> Some FixtureMode.Success
        | [ "nonzero" ] -> Some FixtureMode.Nonzero
        | [ "high-output" ] -> Some FixtureMode.HighOutput
        | [ "echo" ] -> Some FixtureMode.Echo
        | [ "cancellable"; processRecordPath ] -> Some (FixtureMode.Cancellable processRecordPath)
        | _ -> None

[<RequireQualifiedAccess>]
module ProcessFixture =
    let private childFlag = "--fixture-child"

    let private awaitTermination () =
        Thread.Sleep Timeout.Infinite

    let private startChild () =
        let startInfo = ProcessStartInfo(Environment.ProcessPath, UseShellExecute = false)
        startInfo.ArgumentList.Add childFlag
        Process.Start startInfo

    let private execute = function
        | FixtureMode.Success ->
            Console.Out.Write FixtureMode.successOutput
            FixtureMode.successExitCode
        | FixtureMode.Nonzero ->
            Console.Error.Write FixtureMode.failureOutput
            FixtureMode.failureExitCode
        | FixtureMode.HighOutput ->
            Console.Out.Write(String.replicate FixtureMode.highOutputLength "o")
            Console.Error.Write(String.replicate FixtureMode.highOutputLength "e")
            0
        | FixtureMode.Echo ->
            Console.OpenStandardInput().CopyTo(Console.OpenStandardOutput())
            FixtureMode.successExitCode
        | FixtureMode.Cancellable processRecordPath ->
            use childProcess = startChild ()
            File.WriteAllLines(processRecordPath, [ string Environment.ProcessId; string childProcess.Id ])
            awaitTermination ()
            FixtureMode.successExitCode

    let run arguments =
        if arguments |> Array.contains childFlag then
            awaitTermination ()
            0
        else
            match arguments |> List.ofArray |> FixtureMode.parse with
            | Some mode -> execute mode
            | None ->
                eprintfn "Fixture invoked without a recognized mode: %A" arguments
                64
