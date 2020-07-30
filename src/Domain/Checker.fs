namespace MF.Domain

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Text
open MF.TucConsole

[<RequireQualifiedAccess>]
module Checker =
    open MF.TucConsole.ConcurrentCache
    open MF.TucConsole.Option.Operators
    open TypeResolvers

    let check output (results: ParsedDomain list): Result<ResolvedType list, _> =
        Ok []
