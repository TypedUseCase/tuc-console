namespace MF.ErrorHandling

//==============================================
// AsyncResult
//==============================================

type AsyncResult<'Success, 'Error> = Async<Result<'Success, 'Error>>

open System
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module AsyncResult =

    /// Lift a function to AsyncResult
    let map (f: 'SuccessA -> 'SuccessB) (x: AsyncResult<'SuccessA, 'Error>): AsyncResult<'SuccessB, 'Error> =
        Async.map (Result.map f) x

    /// Lift a function to AsyncResult
    let mapError (f: 'ErrorA -> 'ErrorB) (x: AsyncResult<'Success, 'ErrorA>): AsyncResult<'Success, 'ErrorB> =
        Async.map (Result.mapError f) x

    /// Apply ignore to the internal value
    let ignore (x: AsyncResult<'Success, 'Error>): AsyncResult<unit, 'Error> =
        x |> map ignore

    /// Lift a value to AsyncResult
    let retn (x: 'Success): AsyncResult<'Success, 'Error> =
        x |> Result.Ok |> Async.retn

    /// Handles asynchronous exceptions and maps them into Error cases using the provided function
    let catch (f: exn -> 'Error) (x: AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> =
        x
        |> Async.Catch
        |> Async.map(function
            | Choice1Of2 (Ok v) -> Ok v
            | Choice1Of2 (Error err) -> Error err
            | Choice2Of2 ex -> Error (f ex))

    /// Apply an AsyncResult function to an AsyncResult value, monadically
    let applyM (fAsyncResult: AsyncResult<'SuccessA -> 'SuccessB, 'Error>) (xAsyncResult: AsyncResult<'SuccessA, 'Error>): AsyncResult<'SuccessB, 'Error> =
        fAsyncResult |> Async.bind (fun fResult ->
            xAsyncResult |> Async.map (fun xResult -> Result.apply fResult xResult)
        )

    /// Apply an AsyncResult function to an AsyncResult value, applicatively
    let applyA (fAsyncResult: AsyncResult<'SuccessA -> 'SuccessB, 'Error list>) (xAsyncResult: AsyncResult<'SuccessA, 'Error list>): AsyncResult<'SuccessB, 'Error list> =
        fAsyncResult |> Async.bind (fun fResult ->
            xAsyncResult |> Async.map (fun xResult -> Validation.apply fResult xResult)
        )

    /// Apply a monadic function to an AsyncResult value
    let bind (f: 'SuccessA -> AsyncResult<'SuccessB, 'Error>) (xAsyncResult: AsyncResult<'SuccessA, 'Error>): AsyncResult<'SuccessB, 'Error> = async {
        match! xAsyncResult with
        | Ok x -> return! f x
        | Error err -> return (Error err)
    }

    /// Apply a monadic function to an AsyncResult error
    let bindError (f: 'ErrorA -> AsyncResult<'Success, 'ErrorB>) (xAsyncResult: AsyncResult<'Success, 'ErrorA>): AsyncResult<'Success, 'ErrorB> = async {
        match! xAsyncResult with
        | Ok x -> return (Ok x)
        | Error err -> return! f err
    }

    /// Convert a list of AsyncResult into a AsyncResult<list> using monadic style.
    /// Only the first error is returned. The error type NEED NOT be a list.
    let sequenceM (results: AsyncResult<'Success, 'Error> list): AsyncResult<'Success list, 'Error> =
        let (<*>) = applyM
        let (<!>) = map
        let cons head tail = head :: tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = retn [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR results initialValue

    let tee (f: 'Success -> unit) (xAsyncResult: AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> = async {
        let! xResult = xAsyncResult
        return xResult |> Result.tee f
    }

    let teeError (f: 'Error -> unit) (xAsyncResult: AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> = async {
        let! xResult = xAsyncResult
        return xResult |> Result.teeError f
    }

    /// Convert a list of AsyncResult into a AsyncResult<list> using applicative style.
    /// All the errors are returned. The error type MUST be a list.
    let sequenceA (results: AsyncResult<'Success, 'Error list> list): AsyncResult<'Success list, 'Error list> =
        let (<*>) = applyA
        let (<!>) = map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = retn [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR results initialValue

    //-----------------------------------
    // Converting between AsyncResults and other types

    /// Lift a value into an Ok inside a AsyncResult
    let ofSuccess (x: 'Success): AsyncResult<'Success, 'Error> =
        x |> Result.Ok |> Async.retn

    /// Lift a value into an Error inside a AsyncResult
    let ofError (x: 'Error): AsyncResult<'Success, 'Error> =
        x |> Result.Error |> Async.retn

    /// Lift a Result into an AsyncResult
    let ofResult (x: Result<'Success, 'Error>): AsyncResult<'Success, 'Error> =
        x |> Async.retn

    /// Lift a Async into an AsyncResult
    let ofAsync (x: Async<'Success>): AsyncResult<'Success, 'Error> =
        x |> Async.map Result.Ok

    /// Lift a Async into an AsyncResult and handles exception into Result
    let ofAsyncCatch (f: exn -> 'Error) (x: Async<'Success>): AsyncResult<'Success, 'Error> =
        x |> ofAsync |> catch f

    /// Lift a Task into an AsyncResult
    let ofTask (x: Task<'Success>): AsyncResult<'Success, 'Error> =
        x |> Async.AwaitTask |> ofAsync

    /// Lift a Task into an AsyncResult and handles exception into Result
    let ofTaskCatch (f: exn -> 'Error) (x: Task<'Success>): AsyncResult<'Success, 'Error> =
        x |> ofTask |> catch f

    /// Lift a Task into an AsyncResult
    let ofEmptyTask (x: Task): AsyncResult<unit, 'Error> =
        x |> Async.AwaitTask |> ofAsync

    /// Lift a Task into an AsyncResult and handles exception into Result
    let ofEmptyTaskCatch (f: exn -> 'Error) (x: Task): AsyncResult<unit, 'Error> =
        x |> ofEmptyTask |> catch f

    /// Lift an Option into an AsyncResult
    let ofOption (onMissing: 'Error): Option<'Success> -> AsyncResult<'Success, 'Error> = function
        | Some v -> ofSuccess v
        | _ -> ofError onMissing

    /// Lift an async Option into an AsyncResult
    let ofAsyncOption (onMissing: 'Error) (aO: Async<Option<'Success>>): AsyncResult<'Success, 'Error> =
        aO |> ofAsyncCatch (fun _ -> onMissing) |> bind (ofOption onMissing)

    let ofBool (onFalse: 'Error) (b: bool): AsyncResult<unit, 'Error> =
        if b then ofSuccess () else ofError onFalse

    /// Run asyncResults in Parallel, handles the errors and concats results
    let ofParallelAsyncResults<'Success, 'Error> (f: exn -> 'Error) (results: AsyncResult<'Success, 'Error> list): AsyncResult<'Success list, 'Error list> =
        results
        |> List.map (mapError List.singleton)
        |> Async.Parallel
        |> ofAsyncCatch (f >> List.singleton)
        |> bind (
            Seq.toList
            >> Validation.ofResults
            >> Result.mapError List.concat
            >> ofResult
        )

    /// Run asyncs in Parallel, handles the errors and concats results
    let ofParallelAsyncs<'Success, 'Error> (f: exn -> 'Error) (asyncs: Async<'Success> list): AsyncResult<'Success list, 'Error list> =
        asyncs
        |> Async.Parallel
        |> ofAsyncCatch (f >> List.singleton)
        |> map Seq.toList

    /// Run asyncs in limitted Parallel, handles the errors and concats results
    let ofMaxParallelAsyncs<'Success, 'Error> maxParallel (f: exn -> 'Error) (asyncs: Async<'Success> list): AsyncResult<'Success list, 'Error list> =
        asyncs
        |> fun xA -> Async.Parallel(xA, maxParallel)
        |> ofAsyncCatch (f >> List.singleton)
        |> map Seq.toList

    /// Run asyncResults in limitted Parallel, handles the errors and concats results
    let ofMaxParallelAsyncResults<'Success, 'Error> maxParallel (f: exn -> 'Error) (results: AsyncResult<'Success, 'Error> list): AsyncResult<'Success list, 'Error list> =
        results
        |> List.map (mapError List.singleton)
        |> fun xA -> Async.Parallel(xA, maxParallel)
        |> ofAsyncCatch (f >> List.singleton)
        |> bind (
            Seq.toList
            >> Validation.ofResults
            >> Result.mapError List.concat
            >> ofResult
        )

    /// Run asyncResults in Parallel, handles the errors and concats results
    let ofSequentialAsyncResults<'Success, 'Error> (f: exn -> 'Error) (results: AsyncResult<'Success, 'Error> list): AsyncResult<'Success list, 'Error list> =
        results
        |> List.map (mapError List.singleton)
        |> Async.Sequential
        |> ofAsyncCatch (f >> List.singleton)
        |> bind (
            Seq.toList
            >> Validation.ofResults
            >> Result.mapError List.concat
            >> ofResult
        )

    /// Run asyncs in Parallel, handles the errors and concats results
    let ofSequentialAsyncs<'Success, 'Error> (f: exn -> 'Error) (asyncs: Async<'Success> list): AsyncResult<'Success list, 'Error list> =
        asyncs
        |> Async.Sequential
        |> ofAsyncCatch (f >> List.singleton)
        |> map Seq.toList

    //-----------------------------------
    // Utilities lifted from Async

    let sleep (ms: int): AsyncResult<unit, 'Error> =
        Async.Sleep ms |> ofAsync

    module Operators =
        /// AsyncResult.bind
        let inline (>>=) r f = bind f r

        /// AsyncResult.bindError
        let inline (>>-) r f = bindError f r

        /// AsyncResult.tee
        let inline (>>*) r f = tee f r

        /// AsyncResult.teeError
        let inline (>>@) r f = teeError f r

        /// AsyncResult.map
        let inline (<!>) r f = map f r

        /// AsyncResult.mapError
        let inline (<@>) r f = mapError f r

        /// Kleisli composition (composition of 2 functions, which returns an AsyncResult)
        let inline (>=>) fR fR2 =
            fR >> bind fR2

        /// Kleisli composition for errors (composition of 2 functions, which returns an AsyncResult)
        let inline (>->) fR fR2 =
            fR >> bindError fR2

        /// Composition of 2 functions by mapping a Success from 1st function into the 2nd
        let inline (>!>) fR f =
            fR >> map f

        /// Composition of 2 functions by mapping an Error from 1st function into the 2nd
        let inline (>@>) fR fE =
            fR >> mapError fE

        /// Compose with tee function
        let inline (>@*>) fR f =
            fR >> tee f

        /// Compose with tee error function
        let inline (>@@>) fR fE =
            fR >> teeError fE

// ==================================
// AsyncResult computation expression
// ==================================

/// The `asyncResult` computation expression is available globally without qualification
/// See https://github.com/cmeeren/Cvdm.ErrorHandling/blob/master/src/Cvdm.ErrorHandling/AsyncResultBuilder.fs
/// See https://github.com/demystifyfp/FsToolkit.ErrorHandling/blob/master/src/FsToolkit.ErrorHandling/AsyncResultCE.fs
[<AutoOpen>]
module AsyncResultComputationExpression =
    type AsyncResultBuilder() =
        member __.Return (value: 'Success): AsyncResult<'Success, 'Error> =
            async.Return <| result.Return value

        member __.ReturnFrom(asyncResult: AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> =
            asyncResult

        member __.Zero (): AsyncResult<unit, 'Error> =
            async.Return <| result.Zero ()

        member __.Bind (asyncResult: AsyncResult<'SuccessA, 'Error>, (f: 'SuccessA -> AsyncResult<'SuccessB, 'Error>)): AsyncResult<'SuccessB, 'Error> =
            asyncResult |> AsyncResult.bind f

        member __.Delay (generator: unit -> AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> =
            async.Delay generator

        member this.Combine (computation1: AsyncResult<unit, 'Error>, computation2: AsyncResult<'U, 'Error>): AsyncResult<'U, 'Error> =
            this.Bind(computation1, fun () -> computation2)

        member __.TryWith (computation: AsyncResult<'Success, 'Error>, handler: exn -> AsyncResult<'Success, 'Error>): AsyncResult<'Success, 'Error> =
            async.TryWith(computation, handler)

        member __.TryFinally (computation: AsyncResult<'Success, 'Error>, compensation: unit -> unit): AsyncResult<'Success, 'Error> =
            async.TryFinally(computation, compensation)

        member __.Using (resource: 'SuccessA when 'SuccessA :> IDisposable, binder: 'SuccessA -> AsyncResult<'SuccessB, 'Error>): AsyncResult<'SuccessB, 'Error> =
            async.Using(resource, binder)

        member this.While (guard: unit -> bool, computation: AsyncResult<unit, 'Error>): AsyncResult<unit, 'Error> =
            if not <| guard () then this.Zero ()
            else this.Bind(computation, fun () -> this.While (guard, computation))

        member this.For (sequence: #seq<'Success>, binder: 'Success -> AsyncResult<unit, 'Error>): AsyncResult<unit, 'Error> =
            this.Using(sequence.GetEnumerator (), fun enum ->
                this.While(enum.MoveNext,
                    this.Delay(fun () -> binder enum.Current)))

    [<AutoOpen>]
    module AsyncExtensions =

        // Having Async<_> members as extensions gives them lower priority in
        // overload resolution between Async<_> and Async<Result<_,_>>.
        type AsyncResultBuilder with
            member __.ReturnFrom (async: Async<'Success>) : AsyncResult<'Success, exn> =
                async |> AsyncResult.ofAsyncCatch id

            member __.ReturnFrom (task: Task<'Success>) : AsyncResult<'Success, exn> =
                task |> AsyncResult.ofTaskCatch id

            member __.ReturnFrom (task: Task) : AsyncResult<unit, exn> =
                task |> AsyncResult.ofEmptyTaskCatch id

            member this.Bind(async: Async<'SuccessA>, f: 'SuccessA -> AsyncResult<'SuccessB, exn>): AsyncResult<'SuccessB, exn> =
                this.Bind (async |> AsyncResult.ofAsyncCatch id, f)

            member this.Bind(task: Task<'SuccessA>, f: 'SuccessA -> AsyncResult<'SuccessB, exn>): AsyncResult<'SuccessB, exn> =
                this.Bind (task |> AsyncResult.ofTaskCatch id, f)

            member this.Bind(task: Task, f: unit -> AsyncResult<'Success, exn>): AsyncResult<'Success, exn> =
                this.Bind (task |> AsyncResult.ofEmptyTaskCatch id, f)

    [<AutoOpen>]
    module ResultExtensions =

        // Having Result<_> members as extensions gives them lower priority in
        // overload resolution between Result<_> and Async<Result<_,_>>.
        type AsyncResultBuilder with
            member __.ReturnFrom (result: Result<'Success, 'Error>) : AsyncResult<'Success, 'Error> =
                result |> AsyncResult.ofResult

            member this.Bind(result: Result<'SuccessA, 'Error>, f: 'SuccessA -> AsyncResult<'SuccessB, 'Error>): AsyncResult<'SuccessB, 'Error> =
                this.Bind (result |> AsyncResult.ofResult, f)

    let asyncResult = AsyncResultBuilder()

[<AutoOpen>]
module AsyncResultExtension =

    [<RequireQualifiedAccess>]
    module AsyncResult =
        let rec retryWith log waitMs attempts xA =
            if attempts > 0 then
                xA
                |> AsyncResult.bindError (fun _ -> asyncResult {
                    log <| sprintf "Retrying [%A] ..." attempts
                    do! AsyncResult.sleep waitMs
                    return! xA
                })
                |> retryWith log waitMs (attempts - 1)
            else xA

        let rec retry waitMs attempts xA =
            if attempts > 0 then
                xA
                |> AsyncResult.bindError (fun _ -> asyncResult {
                    do! AsyncResult.sleep waitMs
                    return! xA
                })
                |> retry waitMs (attempts - 1)
            else xA
