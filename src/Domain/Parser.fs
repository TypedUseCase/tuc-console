namespace MF.Domain

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open MF.TucConsole.Utils

type ParsedDomain = ParsedDomain of FSharpCheckProjectResults

[<RequireQualifiedAccess>]
module Parser =
    open System
    open System.IO

    let private parseAndCheck (output: MF.ConsoleApplication.Output) (checker: FSharpChecker) (file, input) =
        if output.IsVerbose() then output.Title "ParseAndCheck"

        if output.IsVeryVerbose() then output.Section "GetProjectOptionsFromScript"
        let projOptions, errors =
            checker.GetProjectOptionsFromScript(file, SourceText.ofString input)
            |> Async.RunSynchronously

        if output.IsVeryVerbose() then output.Message "Ok"
        if output.IsDebug() then output.Message <| sprintf "ProjOptions:\n%A" projOptions
        if output.IsDebug() then output.Message <| sprintf "ProjOptions.Errors:\n%A" errors

        if output.IsVeryVerbose() then output.Section "Runtime Dir"
        let runtimeDir = Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
        if output.IsDebug() then output.Message <| sprintf "Runtime Dir: %A" runtimeDir

        if output.IsVeryVerbose() then output.Section "Configure opts"
        let fprojOptions = projOptions

        let addLibFromRTDirIfMissing (libName:string) (opts:string[]) =
            let hasIt = opts |> Array.tryFind (fun opt -> opt.EndsWith(libName))
            if hasIt.IsSome then
                opts
            else
                let lib = Path.Combine(runtimeDir, libName)
                if not (File.Exists(lib)) then failwithf "can't find %A in runtime dir: %A" libName runtimeDir
                if output.IsVeryVerbose() then output.Message <| sprintf "Adding %A to options" lib
                Array.concat [ opts; [| "-r:" + lib |] ]

        let oo =
            fprojOptions.OtherOptions
            |> addLibFromRTDirIfMissing "mscorlib.dll"
            |> addLibFromRTDirIfMissing "netstandard.dll"
            |> addLibFromRTDirIfMissing "System.Runtime.dll"
            |> addLibFromRTDirIfMissing "System.Runtime.Numerics.dll"
            |> addLibFromRTDirIfMissing "System.Private.CoreLib.dll"
            |> addLibFromRTDirIfMissing "System.Collections.dll"
            |> addLibFromRTDirIfMissing "System.Net.Requests.dll"
            |> addLibFromRTDirIfMissing "System.Net.WebClient.dll"

        let fprojOptions = { fprojOptions with OtherOptions = oo }
        if output.IsVeryVerbose() then output.Message "Ok"

        if output.IsVeryVerbose() then output.Section "ParseAndCheckProject"
        checker.ParseAndCheckProject (fprojOptions)
        |> Async.RunSynchronously
        |> tee (fun parseFileResults ->
            if output.IsVeryVerbose() then output.Message "Ok"

            if output.IsDebug() then output.Options "Result:" [
                ["DependencyFiles"; parseFileResults.DependencyFiles |> sprintf "%A"]
                ["Errors"; parseFileResults.Errors |> sprintf "%A"]
                ["ProjectContext"; parseFileResults.ProjectContext |> sprintf "%A"]
            ]
        )

    let parse (output: MF.ConsoleApplication.Output) file =
        let checker = FSharpChecker.Create()    // to allow implementation details add: keepAssemblyContents=true

        if output.IsVerbose() then output.Title <| sprintf "Parse %A" file

        (file, File.ReadAllText file)
        |> parseAndCheck output checker
        |> ParsedDomain
