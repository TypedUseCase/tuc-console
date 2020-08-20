type Name = {
    FirstName: string
    Middle: string option
    LastName: string
}

type Id = UUID

type ContractCreatedEvent = {
    ContractId: Id
    Intent: Intent
    Texts: Text list
}

and Intent = {
    Purpose: string
    Scope: string
}

and Text = Text of string

type Service = {
    FirstMethod: SingleAttribute -> MethodResult
    SecondMethod: SingleAttribute -> Name -> MethodResult
    ThirdMethod: SingleAttribute * Name -> unit
}

and SingleAttribute = SingleAttribute of string

and MethodResult =
    | Ok
    | Error
