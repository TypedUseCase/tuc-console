namespace Tuc.Puml

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Runtime.InteropServices
open System.Security.Cryptography
open System.Text
open System.Threading
open System.Threading.Tasks
open Feather.ErrorHandling

type PlantUmlExecutable = private PlantUmlExecutable of string

type RendererSettings = {
    PlantUmlExecutable: PlantUmlExecutable
}

type Renderer = private {
    PlantUmlExecutable: PlantUmlExecutable
}

/// Executable invocation the process protocol runs.
type internal RenderCommand = {
    Executable: PlantUmlExecutable
    Arguments: string list
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
    | ExecutableNotFound of PlantUmlExecutable
    | UnsupportedRuntime of runtimeId: string
    | RuntimeArchiveNotFound of path: string
    | RuntimeExtractionFailed of message: string
    | StartFailed of message: string
    | RenderingFailed of exitCode: int * message: string
    | CommunicationFailed of message: string
    | Cancelled

[<RequireQualifiedAccess>]
module PlantUmlExecutable =
    let value (PlantUmlExecutable path) = path

    let create path = PlantUmlExecutable path

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
        | RenderError.ExecutableNotFound executable -> sprintf "PlantUML native executable is missing at %s." (PlantUmlExecutable.value executable)
        | RenderError.UnsupportedRuntime runtimeId -> sprintf "PlantUML native runtime is unsupported on %s." runtimeId
        | RenderError.RuntimeArchiveNotFound path -> sprintf "PlantUML native runtime archive is missing at %s." path
        | RenderError.RuntimeExtractionFailed message -> message
        | RenderError.StartFailed message -> message
        | RenderError.RenderingFailed (exitCode, message) -> sprintf "PlantUML exited with code %d: %s" exitCode message
        | RenderError.CommunicationFailed message -> message
        | RenderError.Cancelled -> "PlantUML rendering was cancelled."

[<RequireQualifiedAccess>]
module NativeRuntime =
    [<RequireQualifiedAccess>]
    type private Platform =
        | Linux
        | Windows
        | MacOS
        | Unsupported

    let private extractionLock = obj ()

    let private currentPlatform () =
        if OperatingSystem.IsLinux() then Platform.Linux
        elif OperatingSystem.IsWindows() then Platform.Windows
        elif OperatingSystem.IsMacOS() then Platform.MacOS
        else Platform.Unsupported

    let private currentRuntimeId () =
        match currentPlatform (), RuntimeInformation.OSArchitecture with
        | Platform.Linux, Architecture.X64 -> Some "linux-x64"
        | Platform.Windows, Architecture.X64 -> Some "win-x64"
        | Platform.MacOS, Architecture.Arm64 -> Some "osx-arm64"
        | _ -> None

    let private executableName () = if OperatingSystem.IsWindows() then "plantuml.exe" else "plantuml"

    let private extract archivePath =
        use archive = File.OpenRead archivePath
        let cacheKey = archive |> SHA256.HashData |> Convert.ToHexString |> fun hash -> hash.ToLowerInvariant()
        let destination = Path.Combine(Path.GetTempPath(), "tuc-console", "plantuml", cacheKey)
        let executable = Path.Combine(destination, executableName ())

        try
            if File.Exists executable |> not then
                let temporary = destination + ".tmp-" + Guid.NewGuid().ToString "N"
                Directory.CreateDirectory temporary |> ignore

                try
                    ZipFile.ExtractToDirectory(archivePath, temporary)

                    if OperatingSystem.IsWindows() |> not then
                        File.SetUnixFileMode(Path.Combine(temporary, executableName ()), UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute ||| UnixFileMode.GroupRead ||| UnixFileMode.GroupExecute ||| UnixFileMode.OtherRead ||| UnixFileMode.OtherExecute)

                    lock extractionLock (fun () ->
                        if File.Exists executable |> not then
                            if Directory.Exists destination then Directory.Delete(destination, true)
                            Directory.CreateDirectory(Path.GetDirectoryName destination) |> ignore
                            Directory.Move(temporary, destination)
                    )
                finally
                    if Directory.Exists temporary then Directory.Delete(temporary, true)

            Ok (PlantUmlExecutable.create executable)
        with error ->
            Error (RenderError.RuntimeExtractionFailed error.Message)

    let bundled () =
        match currentRuntimeId () with
        | None -> Error (RenderError.UnsupportedRuntime RuntimeInformation.RuntimeIdentifier)
        | Some runtimeId ->
            let archivePath =
                Path.Combine(AppContext.BaseDirectory, "plantuml", "native", runtimeId, "plantuml.zip")

            if File.Exists archivePath |> not then Error (RenderError.RuntimeArchiveNotFound archivePath)
            else extract archivePath

[<RequireQualifiedAccess>]
module Renderer =
    let internal plantUmlCommand (renderer: Renderer) (format: RenderFormat): RenderCommand = {
        Executable = renderer.PlantUmlExecutable
        Arguments = [ format |> RenderFormat.toCliSwitch; "-pipe"; "-charset"; "UTF-8" ]
    }

    let private startInfo (command: RenderCommand) =
        let startInfo = ProcessStartInfo(PlantUmlExecutable.value command.Executable)
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardInput <- true
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.StandardInputEncoding <- UTF8Encoding false
        startInfo.StandardErrorEncoding <- UTF8Encoding false
        command.Arguments |> List.iter startInfo.ArgumentList.Add
        startInfo

    let private communicationError (error: exn) = error.Message |> RenderError.CommunicationFailed

    let private startProcess (command: RenderCommand): Result<Process, RenderError> =
        try Process.Start(startInfo command) |> Ok
        with error -> Error (RenderError.StartFailed error.Message)

    let private terminateProcessTree (childProcess: Process) =
        try if not childProcess.HasExited then childProcess.Kill true with _ -> ()

    let create (settings: RendererSettings) =
        if settings.PlantUmlExecutable |> PlantUmlExecutable.value |> File.Exists then
            Ok { PlantUmlExecutable = settings.PlantUmlExecutable }
        else
            Error (RenderError.ExecutableNotFound settings.PlantUmlExecutable)

    let private writeSource (standardInput: StreamWriter) (source: string): AsyncResult<unit, RenderError> = async {
        try
            do! standardInput.WriteAsync source |> Async.AwaitTask
            standardInput.Close()
            return Ok ()
        with error -> return Error (communicationError error)
    }

    let private liftCommunication (task: Task): AsyncResult<unit, RenderError> = task |> AsyncResult.ofEmptyTaskCatch communicationError

    let private terminateOnError (childProcess: Process) = function
        | Error _ -> terminateProcessTree childProcess
        | Ok _ -> ()

    let private runWithCancellation (cancellationToken: CancellationToken) (command: RenderCommand) (source: string): AsyncResult<byte[], RenderError> = async {
        match startProcess command with
        | Error error -> return Error error
        | Ok childProcess ->
            use childProcess = childProcess
            use _ = cancellationToken.Register(fun () -> terminateProcessTree childProcess)
            use standardInput = childProcess.StandardInput
            use stdout = new MemoryStream()
            let stdoutRead = childProcess.StandardOutput.BaseStream.CopyToAsync stdout |> liftCommunication
            let stderrRead = childProcess.StandardError.ReadToEndAsync() |> AsyncResult.ofTaskCatch communicationError
            let! sourceWritten = writeSource standardInput source
            let! exited = liftCommunication (childProcess.WaitForExitAsync())
            exited |> terminateOnError childProcess
            let! stdoutDrained = stdoutRead
            let! stderrDrained = stderrRead

            return
                if cancellationToken.IsCancellationRequested then Error RenderError.Cancelled
                else result {
                    do! exited
                    do! stdoutDrained
                    let! stderr = stderrDrained

                    return!
                        match childProcess.ExitCode with
                        | 0 ->
                            result {
                                do! sourceWritten
                                return stdout.ToArray()
                            }
                        | exitCode -> Error (RenderError.RenderingFailed (exitCode, stderr))
                }
    }

    let private withTypedCancellation operation = async {
        return!
            Async.FromContinuations(fun (complete, fail, _) ->
                Async.StartWithContinuations(operation, complete, fail, (fun _ -> complete (Error RenderError.Cancelled)), cancellationToken = CancellationToken.None)
            )
    }

    let internal runCommand (command: RenderCommand) (source: string): AsyncResult<byte[], RenderError> = async {
        let! cancellationToken = Async.CancellationToken
        return! runWithCancellation cancellationToken command source |> withTypedCancellation
    }

    let render (renderer: Renderer) (format: RenderFormat) (source: string) = runCommand (plantUmlCommand renderer format) source
