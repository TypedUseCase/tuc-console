type Function = Function of (string -> string)

type SecondFunction = SecondFunction of (int * string -> unit -> string)

type GenericFunction<'Input, 'Output> = GenericFunction of (string -> 'Input -> 'Output)

type ComplexFunction<'Input> = ComplexFunction of ('Input list -> string option -> Async<Result<'Input, string>>)
