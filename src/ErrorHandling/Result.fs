namespace MF.ErrorHandling

/// Functions for Result type (functor and monad).
/// For applicatives, see Validation.
[<RequireQualifiedAccess>]
module Result =
    let tee (f: 'Success -> unit): Result<'Success, 'Error> -> Result<'Success, 'Error> = function
        | Ok x -> f x; Ok x
        | Error x -> Error x

    let teeError (f: 'Error -> unit): Result<'Success, 'Error> -> Result<'Success, 'Error> = function
        | Ok x -> Ok x
        | Error x -> f x; Error x

    let iter (f: 'Success -> unit) (r: Result<'Success, 'Error>): unit =
        r
        |> tee f
        |> ignore

    let bindError (f: 'ErrorA -> Result<'Success, 'ErrorB>): Result<'Success, 'ErrorA> -> Result<'Success, 'ErrorB> = function
        | Ok x -> Ok x
        | Error x -> f x

    let ofOption (onMissing: 'Error): 'Success option -> Result<'Success, 'Error> = function
        | Some x -> Ok x
        | None -> Error onMissing

    let toOption: Result<'Success, 'Error> -> 'Success option = function
        | Ok x -> Some x
        | _ -> None

    let toChoice: Result<'Success, 'Error> -> Choice<'Success, 'Error> = function
        | Ok x -> Choice1Of2 x
        | Error x -> Choice2Of2 x

    let ofChoice: Choice<'Success, 'Error> -> Result<'Success, 'Error> = function
        | Choice1Of2 x -> Ok x
        | Choice2Of2 x -> Error x

    /// Apply a Result<fn> to a Result<x> monadically
    let apply (fR: Result<'SuccessA -> 'SuccessB, 'Error>) (xR: Result<'SuccessA, 'Error>): Result<'SuccessB, 'Error> =
        match fR, xR with
        | Ok f, Ok x -> Ok (f x)
        | Error err1, Ok _ -> Error err1
        | Ok _, Error err2 -> Error err2
        | Error err1, Error _ -> Error err1

    /// combine a list of results, monadically
    let sequence (results: Result<'Success, 'Error> list): Result<'Success list, 'Error> =
        let (<*>) = apply // monadic
        let (<!>) = Result.map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = Ok [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR results initialValue

    /// combine and concat a list of results, monadically
    let sequenceConcat (results: Result<'Success list, 'Error> list): Result<'Success list, 'Error> =
        results
        |> sequence
        |> Result.map List.concat

    /// select all success results
    let list (results: Result<'Success, 'Error> list): 'Success list =
        results
        |> List.choose (function
            | Ok success -> Some success
            | _ -> None
        )

    /// collect all success results
    let listCollect (results: Result<'Success list, 'Error> list): 'Success list =
        results
        |> List.collect (function
            | Ok success -> success
            | _ -> []
        )

    /// Convert a Result option to Result of Option
    let option: Result<'Success, 'Error> option -> Result<'Success option, 'Error> = function
        | Some (Ok success) -> Ok (Some success)
        | Some (Error error) -> Error error
        | None -> Ok None

    /// return a success or fail with exception
    let orFail = function
        | Ok success -> success
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
        let inline (>@>) fR fE =
            fR >> Result.mapError fE

        /// Compose with tee function
        let inline (>@*>) fR f =
            fR >> tee f

        /// Compose with tee error function
        let inline (>@@>) fR fE =
            fR >> teeError fE

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
