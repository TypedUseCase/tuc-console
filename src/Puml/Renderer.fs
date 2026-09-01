namespace Tuc.Puml

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Feather.ErrorHandling

type JavaExecutable = private JavaExecutable of string

type PlantUmlJar = private PlantUmlJar of string

type private AvailablePlantUmlJar = private AvailablePlantUmlJar of PlantUmlJar

type RendererSettings = {
    PlantUmlJar: PlantUmlJar
}

type Renderer = private {
    JavaExecutable: JavaExecutable
    PlantUmlJar: AvailablePlantUmlJar
}

/// Executable invocation the process protocol runs; the testable boundary beneath PlantUML rendering.
type internal RenderCommand = {
    Executable: JavaExecutable
    Arguments: string list
}

type internal JavaEnvironment = {
    JavaHome: string option
    Path: string option
}

[<RequireQualifiedAccess>]
type RenderFormat =
    | Png
    | Svg
    | Eps
    | Pdf
    | Vdx
    | Xmi
    | Scxml
    | Html
    | Ascii
    | AsciiUnicode
    | LaTeX

[<RequireQualifiedAccess>]
type RenderError =
    | JavaNotFound
    | JarNotFound of PlantUmlJar
    | StartFailed of message: string
    | RenderingFailed of exitCode: int * message: string
    | CommunicationFailed of message: string
    | Cancelled

[<RequireQualifiedAccess>]
module JavaExecutable =
    let value (JavaExecutable path) = path

    let create path =
        if File.Exists path then
            Ok (JavaExecutable path)
        else
            Error RenderError.JavaNotFound

[<RequireQualifiedAccess>]
module PlantUmlJar =
    let value (PlantUmlJar path) = path

    let create path = PlantUmlJar path

module private AvailablePlantUmlJar =
    let value (AvailablePlantUmlJar jar) = jar |> PlantUmlJar.value

    let create jar =
        if jar |> PlantUmlJar.value |> File.Exists then
            Ok (AvailablePlantUmlJar jar)
        else
            Error (RenderError.JarNotFound jar)

[<RequireQualifiedAccess>]
module RenderFormat =
    let toCliSwitch = function
        | RenderFormat.Png -> "-tpng"
        | RenderFormat.Svg -> "-tsvg"
        | RenderFormat.Eps -> "-teps"
        | RenderFormat.Pdf -> "-tpdf"
        | RenderFormat.Vdx -> "-tvdx"
        | RenderFormat.Xmi -> "-txmi"
        | RenderFormat.Scxml -> "-tscxml"
        | RenderFormat.Html -> "-thtml"
        | RenderFormat.Ascii -> "-ttxt"
        | RenderFormat.AsciiUnicode -> "-tutxt"
        | RenderFormat.LaTeX -> "-tlatex"

[<RequireQualifiedAccess>]
module RenderError =
    let format = function
        | RenderError.JavaNotFound -> "PlantUML local rendering requires Java on PATH or JAVA_HOME."
        | RenderError.JarNotFound jar -> sprintf "PlantUML JAR is missing at %s." (PlantUmlJar.value jar)
        | RenderError.StartFailed message -> message
        | RenderError.RenderingFailed (exitCode, message) -> sprintf "PlantUML exited with code %d: %s" exitCode message
        | RenderError.CommunicationFailed message -> message
        | RenderError.Cancelled -> "PlantUML rendering was cancelled."

[<RequireQualifiedAccess>]
module Renderer =
    let internal plantUmlCommand (renderer: Renderer) (format: RenderFormat): RenderCommand = {
        Executable = renderer.JavaExecutable
        Arguments = [
            "-Dfile.encoding=UTF-8"
            "-jar"
            renderer.PlantUmlJar |> AvailablePlantUmlJar.value
            format |> RenderFormat.toCliSwitch
            "-pipe"
            "-charset"
            "UTF-8"
        ]
    }

    let private startInfo (command: RenderCommand) =
        let startInfo = ProcessStartInfo(JavaExecutable.value command.Executable)
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardInput <- true
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.StandardInputEncoding <- UTF8Encoding false
        startInfo.StandardErrorEncoding <- UTF8Encoding false

        command.Arguments
        |> List.iter startInfo.ArgumentList.Add

        startInfo

    let private communicationError (error: exn) =
        error.Message
        |> RenderError.CommunicationFailed

    let private startProcess (command: RenderCommand): Result<Process, RenderError> =
        try
            Process.Start(startInfo command)
            |> Ok
        with error ->
            Error (RenderError.StartFailed error.Message)

    let private terminateProcessTree (childProcess: Process) =
        try
            if not childProcess.HasExited then
                childProcess.Kill true
        with _ ->
            ()

    let internal createWith (findJava: unit -> Result<JavaExecutable, RenderError>) (settings: RendererSettings): Result<Renderer, RenderError> =
        settings.PlantUmlJar
        |> AvailablePlantUmlJar.create
        |> Result.bind (fun jar ->
            findJava ()
            |> Result.map (fun java ->
                {
                    JavaExecutable = java
                    PlantUmlJar = jar
                }
            )
        )

    let internal discoverJavaWith executable (environment: JavaEnvironment) =
        let javaHome =
            environment.JavaHome
            |> Option.map (fun home -> Path.Combine(home, "bin", executable))
            |> Option.bind (JavaExecutable.create >> Result.toOption)

        let fromPath =
            environment.Path
            |> Option.bind (fun path ->
                path.Split Path.PathSeparator
                |> Array.tryPick (fun directory ->
                    Path.Combine(directory, executable)
                    |> JavaExecutable.create
                    |> Result.toOption
                )
            )

        match javaHome |> Option.orElse fromPath with
        | Some executable -> Ok executable
        | None -> Error RenderError.JavaNotFound

    let private environmentVariable variable =
        Environment.GetEnvironmentVariable variable
        |> Option.ofObj

    let discoverJava () =
        let executable = if OperatingSystem.IsWindows() then "java.exe" else "java"

        {
            JavaHome = environmentVariable "JAVA_HOME"
            Path = environmentVariable "PATH"
        }
        |> discoverJavaWith executable

    let create = createWith discoverJava

    let private writeSource (standardInput: StreamWriter) (source: string): AsyncResult<unit, RenderError> = async {
        try
            do! standardInput.WriteAsync source |> Async.AwaitTask
            standardInput.Close()
            return Ok ()
        with error ->
            return Error (communicationError error)
    }

    let private liftCommunication (task: Task): AsyncResult<unit, RenderError> =
        task |> AsyncResult.ofEmptyTaskCatch communicationError

    let private terminateOnError (childProcess: Process) = function
        | Error _ -> terminateProcessTree childProcess
        | Ok _ -> ()

    // Every step is total, so the flow always reaches the drains and disposal never races a live read.
    let private runWithCancellation (cancellationToken: CancellationToken) (command: RenderCommand) (source: string): AsyncResult<byte[], RenderError> = async {
        match startProcess command with
        | Error error -> return Error error
        | Ok childProcess ->
            // Register owned resources, make cancellation kill the process tree
            use childProcess = childProcess
            use _ = cancellationToken.Register(fun () -> terminateProcessTree childProcess)
            use standardInput = childProcess.StandardInput
            use stdout = new MemoryStream()

            // Start draining both output pipes
            let stdoutRead = childProcess.StandardOutput.BaseStream.CopyToAsync stdout |> liftCommunication
            let stderrRead = childProcess.StandardError.ReadToEndAsync() |> AsyncResult.ofTaskCatch communicationError

            // Failures don't short-circuit here to prevent resource leaks, but they do kill the process tree to let the drains complete in the next stage
            let! sourceWritten = writeSource standardInput source
            sourceWritten |> terminateOnError childProcess
            let! exited = liftCommunication (childProcess.WaitForExitAsync())
            exited |> terminateOnError childProcess

            // The process has terminated, wait for both output drains to complete
            let! stdoutDrained = stdoutRead
            let! stderrDrained = stderrRead

            // Check cancellation, then the protocol results, then the exit code
            return
                if cancellationToken.IsCancellationRequested then Error RenderError.Cancelled
                else result {
                    do! sourceWritten
                    do! exited
                    do! stdoutDrained
                    let! stderr = stderrDrained

                    return!
                        match childProcess.ExitCode with
                        | 0 -> Ok (stdout.ToArray())
                        | exitCode -> Error (RenderError.RenderingFailed (exitCode, stderr))
                }
    }

    // Shields the operation from the caller's cancellation: aborting it mid-flight would abandon
    // process cleanup, so it always runs to completion and reports cancellation as a typed result
    // instead of an exception.
    let private withTypedCancellation operation = async {
        return!
            Async.FromContinuations(fun (complete, fail, _) ->
                Async.StartWithContinuations(
                    operation,
                    complete,
                    fail,
                    (fun _ -> complete (Error RenderError.Cancelled)),
                    cancellationToken = CancellationToken.None
                )
            )
    }

    let internal runCommand (command: RenderCommand) (source: string): AsyncResult<byte[], RenderError> = async {
        let! cancellationToken = Async.CancellationToken

        return!
            runWithCancellation cancellationToken command source
            |> withTypedCancellation
    }

    let render (renderer: Renderer) (format: RenderFormat) (source: string) =
        runCommand (plantUmlCommand renderer format) source
