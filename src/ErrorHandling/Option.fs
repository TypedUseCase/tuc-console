namespace ErrorHandling

[<RequireQualifiedAccess>]
module Option =
    open System

    let retn x = Some x

    let mapNone f = function
        | Some v -> Some v
        | None -> f ()

    let ofChoice = function
        | Choice1Of2 x -> Some x
        | _ -> None

    let toChoice case2 = function
        | Some x -> Choice1Of2 x
        | None -> Choice2Of2 (case2 ())

    let ofNullable (nullable: Nullable<'a>): 'a option =
        match box nullable with
        | null -> None // CLR null
        | :? Nullable<'a> as n when not n.HasValue -> None // CLR struct
        | :? Nullable<'a> as n when n.HasValue -> Some (n.Value) // CLR struct
        | x when x.Equals (DBNull.Value) -> None // useful when reading from the db into F#
        | x -> Some (unbox x) // anything else

    let toNullable = function
        | Some item -> new Nullable<_>(item)
        | None -> new Nullable<_>()

    let orDefault x = function
        | None -> x ()
        | Some y -> y

    let tee f = function
        | Some x -> f x; Some x
        | None -> None

    let teeNone f = function
        | Some x -> Some x
        | None -> f(); None

    let toResult = function
        | Some (Ok success) -> Ok (Some success)
        | Some (Error error) -> Error error
        | None -> Ok None

    module Operators =
        /// Option.bind
        let inline (>>=) o f = Option.bind f o

        /// Option.tee
        let inline (>>*) o f = tee f o

        /// Option.teeNone
        let inline (>>@) o f = teeNone f o

        /// Option.map
        let inline (<!>) o f = Option.map f o

        /// Option.mapNone
        let inline (<@>) o f = mapNone f o

        /// Option.defaultValue - if value is None, default value will be used
        let (<?=>) defaultValue o = Option.defaultValue o defaultValue

        /// Option.orElse - if value is None, other option will be used
        let (<??>) other o = Option.orElse o other

        /// Result.ofOption - if value is None, error will be returned
        let (<?!>) o error = o |> Result.ofOption error

        /// Option.iter
        let (|>!) o f = o |> Option.iter f

[<AutoOpen>]
module OptionComputationExpression =
    type MaybeBuilder() =
        member __.Return(x) = Some x
        member __.Bind(x, f) = Option.bind f x

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

    let maybe = MaybeBuilder()
