namespace MF.ErrorHandling

//==============================================
// Async utilities
//==============================================

[<RequireQualifiedAccess>]
module Async =

    /// Lift a function to Async
    let map (f: 'a -> 'b) (xA: Async<'a>): Async<'b> = async {
        let! x = xA
        return f x
    }

    /// Lift a value to Async
    let retn (x: 'a): Async<'a> =
        async.Return x

    /// Apply an Async function to an Async value
    let apply (fA: Async<'a -> 'b>) (xA: Async<'a>): Async<'b> = async {
        // start the two asyncs in parallel
        let! fChild = Async.StartChild fA  // run in parallel
        let! x = xA

        // wait for the result of the first one
        let! f = fChild
        return f x
    }

    /// Apply a monadic function to an Async value
    let bind (f: 'a -> Async<'b>) (xA: Async<'a>): Async<'b> = async.Bind(xA, f)

    let tee (f: 'a -> unit) (xA: Async<'a>): Async<'a> = async {
        let! x = xA
        f x
        return x
    }
