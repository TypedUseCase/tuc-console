#load "records.fsx"
open Records

module Example =
    let name = {
        FirstName = "First"
        Middle = None
        LastName = "Last"
    }

    let printName name =
        printfn "%s (%s) %s" name.FirstName (name.Middle |> Option.defaultValue "-") name.LastName

    name |> printName
