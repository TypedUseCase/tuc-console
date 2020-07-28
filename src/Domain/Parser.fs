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

[<RequireQualifiedAccess>]
module Dump =
    type private Dump<'Type> = MF.ConsoleApplication.Output -> 'Type -> unit
    type private DumpMany<'Type> = MF.ConsoleApplication.Output -> 'Type seq -> unit

    type private Format<'Type> = 'Type -> string

    let rec private formatType: Format<FSharpType> = function
        | abbreviationType when abbreviationType.IsAbbreviation && abbreviationType.HasTypeDefinition ->
            let generics =
                match abbreviationType.GenericArguments |> Seq.toList with
                | [] -> ""
                | [ generic ] -> generic |> formatType |> sprintf "%s "
                | generics -> generics |> List.map formatType |> String.concat "; " |> sprintf "%s "

            sprintf "%s%s" generics abbreviationType.TypeDefinition.DisplayName

        | functionType when functionType.IsFunctionType ->
            functionType.GenericArguments |> Seq.map formatType |> String.concat " -> "

        | tupleType when tupleType.IsTupleType ->
            tupleType.GenericArguments |> Seq.map formatType |> String.concat " * "

        | genericType when genericType.IsGenericParameter ->
            "'" + genericType.GenericParameter.FullName

        | withtoutTypeDefinition when withtoutTypeDefinition.HasTypeDefinition |> not ->
            [
                if withtoutTypeDefinition.IsAbbreviation then yield withtoutTypeDefinition.IsAbbreviation |> sprintf "IsAbbreviation: %A"
                if withtoutTypeDefinition.IsAnonRecordType then yield withtoutTypeDefinition.IsAnonRecordType |> sprintf "IsAnonRecordType: %A"
                if withtoutTypeDefinition.IsFunctionType then yield withtoutTypeDefinition.IsFunctionType |> sprintf "IsFunctionType: %A"
                if withtoutTypeDefinition.IsGenericParameter then yield withtoutTypeDefinition.IsGenericParameter |> sprintf "IsGenericParameter: %A"
                if withtoutTypeDefinition.IsStructTupleType then yield withtoutTypeDefinition.IsStructTupleType |> sprintf "IsStructTupleType: %A"
                if withtoutTypeDefinition.IsTupleType then yield withtoutTypeDefinition.IsTupleType |> sprintf "IsTupleType: %A"
                if withtoutTypeDefinition.IsUnresolved then yield withtoutTypeDefinition.IsUnresolved |> sprintf "IsUnresolved: %A"
            ]
            |> String.concat "; "
            |> sprintf "<c:yellow>%A (%A)</c>" withtoutTypeDefinition

        | t ->
            let generics =
                t.GenericArguments
                |> Seq.map formatType
                |> Seq.toList
                |> function
                    | [] -> ""
                    | generics -> generics |> String.concat ", " |> sprintf "<%s>"

            [
                if t.IsAbbreviation then yield t.IsAbbreviation |> sprintf "IsAbbreviation: %A"
                if t.IsAnonRecordType then yield t.IsAnonRecordType |> sprintf "IsAnonRecordType: %A"
                if t.IsFunctionType then yield t.IsFunctionType |> sprintf "IsFunctionType: %A"
                if t.IsGenericParameter then yield t.IsGenericParameter |> sprintf "IsGenericParameter: %A"
                if t.IsStructTupleType then yield t.IsStructTupleType |> sprintf "IsStructTupleType: %A"
                if t.IsTupleType then yield t.IsTupleType |> sprintf "IsTupleType: %A"
                if t.IsUnresolved then yield t.IsUnresolved |> sprintf "IsUnresolved: %A"
            ]
            |> String.concat "; "
            |> function | "" -> "" | opts -> sprintf " <c:red>(%s)</c>" opts
            |> sprintf "%s%s%s" t.TypeDefinition.DisplayName generics

    let private dumpFields: DumpMany<FSharpField> = fun output fields ->
        if fields |> Seq.isEmpty |> not then
            fields |> Seq.length |> sprintf "Fields [%d]" |> output.Section

            fields
            |> Seq.iter (fun f ->
                [[
                    f.FullName
                    f.DisplayName
                    f.FieldType |> formatType
                ]]
                |> output.Table [
                    "FullName"
                    "DisplayName"
                    "FieldType"
                ]
            )

    let private dumpMemberOrFunctionOrValues: DumpMany<FSharpMemberOrFunctionOrValue> = fun output mfvs ->
        if mfvs |> Seq.isEmpty |> not then
            mfvs |> Seq.length |> sprintf "MemberOrFunctionOrValue [%d]" |> output.Section

            mfvs
            |> Seq.map (fun mfv -> [
                mfv.FullName
                mfv.DisplayName
            ])
            |> Seq.toList
            |> output.Table [
                "FullName"
                "DisplayName"
            ]

    let private dumpUnionCases: DumpMany<FSharpUnionCase> = fun output unionCases ->
        if unionCases |> Seq.isEmpty |> not then
            unionCases |> Seq.length |> sprintf "e.UnionCases [%d]" |> output.Section

            unionCases
            |> Seq.iter (fun uc ->
                [[
                    uc.FullName
                    uc.DisplayName
                    uc.HasFields |> sprintf "%A"
                    uc.ReturnType |> formatType
                ]]
                |> output.Table [
                    "FullName"
                    "DisplayName"
                    "HasFields"
                    "Returns"
                ]

                uc.UnionCaseFields |> dumpFields output
            )

    let rec private dumpEntities parent: DumpMany<FSharpEntity> = fun output entities ->
        if entities |> Seq.isEmpty |> not then
            entities |> Seq.length |> sprintf "%sEntities [%d]" parent |> output.Section

            entities
            |> Seq.iter (fun e ->
                let accessPath = try e.AccessPath |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"
                let displayName = try e.DisplayName |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"
                let fullName = try e.FullName |> sprintf "%A" with e -> e.Message |> sprintf "<c:red>%s</c>"

                [[
                    fullName
                    accessPath
                    displayName
                ]]
                |> output.Table [
                    "FullName"
                    "AccessPath"
                    "DisplayName"
                ]

                e.FSharpFields |> dumpFields output
                e.MembersFunctionsAndValues |> dumpMemberOrFunctionOrValues output
                e.NestedEntities |> dumpEntities (e.FullName + ".") output
                e.UnionCases |> dumpUnionCases output

                "=" |> String.replicate 60 |> sprintf "<c:gray>%s</c>" |> output.Message
                output.NewLine()
            )

    let parsedDomain output (ParsedDomain results) =
        results.AssemblySignature.Entities
        |> dumpEntities "" output
