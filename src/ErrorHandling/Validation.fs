namespace MF.ErrorHandling

type Validation<'Success, 'Failure> = Result<'Success, 'Failure list>

/// Functions for the `Validation` type (mostly applicative)
[<RequireQualifiedAccess>]
module Validation =

    /// Apply a Validation<fn> to a Validation<x> applicatively
    let apply (fV: Validation<'SuccessA -> 'SuccessB, 'Failure>) (xV: Validation<'SuccessA, 'Failure>): Validation<'SuccessB, 'Failure> =
        match fV, xV with
        | Ok f, Ok x -> Ok (f x)
        | Error errs1, Ok _ -> Error errs1
        | Ok _, Error errs2 -> Error errs2
        | Error errs1, Error errs2 -> Error (errs1 @ errs2)

    // combine a list of Validation, applicatively
    let sequence (validations: Validation<'Success, 'Failure> list): Validation<'Success list, 'Failure> =
        let (<*>) = apply
        let (<!>) = Result.map
        let cons head tail = head::tail
        let consR headR tailR = cons <!> headR <*> tailR
        let initialValue = Ok [] // empty list inside Result

        // loop through the list, prepending each element
        // to the initial value
        List.foldBack consR validations initialValue

    //-----------------------------------
    // Converting between Validations and other types

    let ofResult (xR: Result<'Success, 'Failure>): Validation<'Success, 'Failure> =
        xR |> Result.mapError List.singleton

    let ofResults (xR: Result<'Success, 'Failure> list): Validation<'Success list, 'Failure> =
        xR
        |> List.map ofResult
        |> sequence

    let toResult (xV: Validation<'Success, 'Failure>): Result<'Success, 'Failure list> =
        xV
