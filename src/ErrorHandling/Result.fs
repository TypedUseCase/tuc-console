namespace ErrorHandling

/// Functions for Result type (functor and monad).
/// For applicatives, see Validation.
[<RequireQualifiedAccess>]  // RequireQualifiedAccess forces the `Result.xxx` prefix to be used
module Result =
    let tee f = function
        | Ok x -> f x; Ok x
        | Error x -> Error x

    let teeError f = function
        | Ok x -> Ok x
        | Error x -> f x; Error x

    let iter f r =
        r
        |> tee f
        |> ignore

    let bindError (f: 'a -> Result<'c, 'b>) (v: Result<'c, 'a>) =
        match v with
        | Ok x -> Ok x
        | Error x -> f x

    let ofOption onMissing = function
        | Some x -> Ok x
        | None -> Error onMissing

    let toOption = function
        | Ok x -> Some x
        | _ -> None

    let toChoice = function
        | Ok x -> Choice1Of2 x
        | Error x -> Choice2Of2 x

    let ofChoice = function
        | Choice1Of2 x -> Ok x
        | Choice2Of2 x -> Error x

    /// Apply a Result<fn> to a Result<x> monadically
    let apply fR xR =
        match fR, xR with
        | Ok f, Ok x -> Ok (f x)
        | Error err1, Ok _ -> Error err1
        | Ok _, Error err2 -> Error err2
        | Error err1, Error _ -> Error err1

    /// combine a list of results, monadically
    let sequence aListOfResults =
        let (<*>) = apply // monadic
        let (<!>) = Result.map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = Ok [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR aListOfResults initialValue

    let sequenceConcat aListOfResults =
        aListOfResults
        |> sequence
        |> Result.map List.concat

    let list aListOfResults =
        aListOfResults
        |> List.choose (function
            | Ok success -> Some success
            | _ -> None
        )

    let listCollect aListOfResults =
        aListOfResults
        |> List.collect (function
            | Ok success -> success
            | _ -> []
        )

    let orFail = function
        | Ok option -> option
        | Error e -> failwithf "Error %A" e

    module Operators =
        /// Result.bind
        let inline (>>=) r f = Result.bind f r

        /// Result.bindError
        let inline (>>-) r f = bindError f r

        /// Result.tee
        let inline (>>*) r f = tee f r

        /// Result.teeError
        let inline (>>@) r f = teeError f r

        /// Result.apply
        let inline (<*>) r f = apply f r

        /// Result.map
        let inline (<!>) r f = Result.map f r

        /// Result.mapError
        let inline (<@>) r f = Result.mapError f r

        /// Kleisli composition (composition of 2 functions, which returns a Result)
        let inline (>=>) fR fR2 =
            fR >> Result.bind fR2

        /// Kleisli composition for errors (composition of 2 functions, which returns a Result)
        let inline (>->) fR fR2 =
            fR >> bindError fR2

        /// Composition of 2 functions by mapping a Success from 1st function into the 2nd
        let inline (>!>) fR f =
            fR >> Result.map f

        /// Composition of 2 functions by mapping an Error from 1st function into the 2nd
        let (>@>) fR fE =
            fR >> Result.mapError fE

[<AutoOpen>]
module ResultComputationExpression =
    // https://github.com/swlaschin/DomainModelingMadeFunctional/blob/master/src/OrderTaking/Result.fs#L178

    type ResultBuilder() =
        member __.Return(x) = Ok x
        member __.Bind(x, f) = Result.bind f x

        member __.ReturnFrom(x) = x
        member this.Zero() = this.Return ()

        member __.Delay(f) = f
        member __.Run(f) = f()

        member this.While(guard, body) =
            if not (guard())
            then this.Zero()
            else this.Bind( body(), fun () ->
                this.While(guard, body))

        member this.TryWith(body, handler) =
            try this.ReturnFrom(body())
            with e -> handler e

        member this.TryFinally(body, compensation) =
            try this.ReturnFrom(body())
            finally compensation()

        member this.Using(disposable:#System.IDisposable, body) =
            let body' = fun () -> body disposable
            this.TryFinally(body', fun () ->
                match disposable with
                    | null -> ()
                    | disp -> disp.Dispose())

        member this.For(sequence:seq<_>, body) =
            this.Using(sequence.GetEnumerator(),fun enum ->
                this.While(enum.MoveNext,
                    this.Delay(fun () -> body enum.Current)))

        member this.Combine (a,b) =
            this.Bind(a, fun () -> b())

    let result = ResultBuilder()

type Validation<'Success,'Failure> =
    Result<'Success,'Failure list>

/// Functions for the `Validation` type (mostly applicative)
[<RequireQualifiedAccess>]  // RequireQualifiedAccess forces the `Validation.xxx` prefix to be used
module Validation =

    /// Apply a Validation<fn> to a Validation<x> applicatively
    let apply (fV:Validation<_, _>) (xV:Validation<_, _>) :Validation<_, _> =
        match fV, xV with
        | Ok f, Ok x -> Ok (f x)
        | Error errs1, Ok _ -> Error errs1
        | Ok _, Error errs2 -> Error errs2
        | Error errs1, Error errs2 -> Error (errs1 @ errs2)

    // combine a list of Validation, applicatively
    let sequence (aListOfValidations:Validation<_, _> list) =
        let (<*>) = apply
        let (<!>) = Result.map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = Ok [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR aListOfValidations initialValue

    //-----------------------------------
    // Converting between Validations and other types

    let ofResult xR :Validation<_, _> =
        xR |> Result.mapError List.singleton

    let ofResults xR :Validation<_, _> =
        xR
        |> List.map ofResult
        |> sequence

    let toResult (xV:Validation<_, _>) :Result<_, _> =
        xV

//==============================================
// Async utilities
//==============================================

[<RequireQualifiedAccess>]  // RequireQualifiedAccess forces the `Async.xxx` prefix to be used
module Async =

    /// Lift a function to Async
    let map f xA =
        async {
        let! x = xA
        return f x
        }

    /// Lift a value to Async
    let retn x =
        async.Return x

    /// Apply an Async function to an Async value
    let apply fA xA =
        async {
         // start the two asyncs in parallel
        let! fChild = Async.StartChild fA  // run in parallel
        let! x = xA
        // wait for the result of the first one
        let! f = fChild
        return f x
        }

    /// Apply a monadic function to an Async value
    let bind f xA = async.Bind(xA,f)


//==============================================
// AsyncResult
//==============================================

type AsyncResult<'Success,'Failure> =
    Async<Result<'Success,'Failure>>

[<RequireQualifiedAccess>]  // RequireQualifiedAccess forces the `AsyncResult.xxx` prefix to be used
module AsyncResult =

    /// Lift a function to AsyncResult
    let map f (x:AsyncResult<_, _>) : AsyncResult<_, _> =
        Async.map (Result.map f) x

    /// Lift a function to AsyncResult
    let mapError f (x:AsyncResult<_, _>) : AsyncResult<_, _> =
        Async.map (Result.mapError f) x

    /// Apply ignore to the internal value
    let ignore x =
        x |> map ignore

    /// Lift a value to AsyncResult
    let retn x : AsyncResult<_, _> =
        x |> Result.Ok |> Async.retn

    /// Handles asynchronous exceptions and maps them into Failure cases using the provided function
    let catch f (x:AsyncResult<_, _>) : AsyncResult<_, _> =
        x
        |> Async.Catch
        |> Async.map(function
            | Choice1Of2 (Ok v) -> Ok v
            | Choice1Of2 (Error err) -> Error err
            | Choice2Of2 ex -> Error (f ex))


    /// Apply an AsyncResult function to an AsyncResult value, monadically
    let applyM (fAsyncResult : AsyncResult<_, _>) (xAsyncResult : AsyncResult<_, _>) :AsyncResult<_, _> =
        fAsyncResult |> Async.bind (fun fResult ->
        xAsyncResult |> Async.map (fun xResult -> Result.apply fResult xResult))

    /// Apply an AsyncResult function to an AsyncResult value, applicatively
    let applyA (fAsyncResult : AsyncResult<_, _>) (xAsyncResult : AsyncResult<_, _>) :AsyncResult<_, _> =
        fAsyncResult |> Async.bind (fun fResult ->
        xAsyncResult |> Async.map (fun xResult -> Validation.apply fResult xResult))

    /// Apply a monadic function to an AsyncResult value
    let bind (f: 'a -> AsyncResult<'b,'c>) (xAsyncResult : AsyncResult<_, _>) :AsyncResult<_, _> = async {
        let! xResult = xAsyncResult
        match xResult with
        | Ok x -> return! f x
        | Error err -> return (Error err)
        }

    /// Apply a monadic function to an AsyncResult error
    let bindError (f: 'a -> AsyncResult<'b,'c>) (xAsyncResult : AsyncResult<_, _>) :AsyncResult<_, _> = async {
        let! xResult = xAsyncResult
        match xResult with
        | Ok x -> return (Ok x)
        | Error err -> return! f err
        }

    /// Convert a list of AsyncResult into a AsyncResult<list> using monadic style.
    /// Only the first error is returned. The error type need not be a list.
    let sequenceM resultList =
        let (<*>) = applyM
        let (<!>) = map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = retn [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR resultList  initialValue

    let tee f (xAsyncResult: AsyncResult<_, _>): AsyncResult<_, _> =
        async {
            let! xResult = xAsyncResult
            return xResult |> Result.tee f
        }

    let teeError f (xAsyncResult: AsyncResult<_, _>): AsyncResult<_, _> =
        async {
            let! xResult = xAsyncResult
            return xResult |> Result.teeError f
        }

    /// Convert a list of AsyncResult into a AsyncResult<list> using applicative style.
    /// All the errors are returned. The error type must be a list.
    let sequenceA resultList =
        let (<*>) = applyA
        let (<!>) = map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = retn [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR resultList  initialValue

    //-----------------------------------
    // Converting between AsyncResults and other types

    /// Lift a value into an Ok inside a AsyncResult
    let ofSuccess x : AsyncResult<_, _> =
        x |> Result.Ok |> Async.retn

    /// Lift a value into an Error inside a AsyncResult
    let ofError x : AsyncResult<_, _> =
        x |> Result.Error |> Async.retn

    /// Lift a Result into an AsyncResult
    let ofResult x : AsyncResult<_, _> =
        x |> Async.retn

    /// Lift an Option into an AsyncResult
    let ofOption e x : AsyncResult<_, _> =
        x |> Result.ofOption e |> ofResult

    /// Lift a Async into an AsyncResult
    let ofAsync x : AsyncResult<_, _> =
        x |> Async.map Result.Ok

    /// Lift a Async into an AsyncResult and handles exception into Result
    let ofAsyncCatch f x : AsyncResult<_, _> =
        x |> ofAsync |> catch f

    /// Lift a Task into an AsyncResult
    let ofTask x : AsyncResult<_, _> =
        x |> Async.AwaitTask |> ofAsync

    /// Lift a Task into an AsyncResult and handles exception into Result
    let ofTaskCatch f x : AsyncResult<_, _> =
        x |> ofTask |> catch f

    //-----------------------------------
    // Utilities lifted from Async

    let sleep (ms: int) =
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

// ==================================
// AsyncResult computation expression
// ==================================

/// The `asyncResult` computation expression is available globally without qualification
[<AutoOpen>]
module AsyncResultComputationExpression =

    type AsyncResultBuilder() =
        member __.Return(x) = AsyncResult.retn x
        member __.Bind(x, f) = AsyncResult.bind f x

        member __.ReturnFrom(x) = x
        member this.Zero() = this.Return ()

        member __.Delay(f) = f
        member __.Run(f) = f()

        member this.While(guard, body) =
            if not (guard())
            then this.Zero()
            else this.Bind( body(), fun () ->
                this.While(guard, body))

        member this.TryWith(body, handler) =
            try this.ReturnFrom(body())
            with e -> handler e

        member this.TryFinally(body, compensation) =
            try this.ReturnFrom(body())
            finally compensation()

        member this.Using(disposable:#System.IDisposable, body) =
            let body' = fun () -> body disposable
            this.TryFinally(body', fun () ->
                match disposable with
                    | null -> ()
                    | disp -> disp.Dispose())

        member this.For(sequence:seq<_>, body) =
            this.Using(sequence.GetEnumerator(),fun enum ->
                this.While(enum.MoveNext,
                    this.Delay(fun () -> body enum.Current)))

        member this.Combine (a,b) =
            this.Bind(a, fun () -> b())

    let asyncResult = AsyncResultBuilder()
