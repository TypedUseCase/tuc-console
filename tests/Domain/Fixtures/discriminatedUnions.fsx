type FirstType =
    | CaseA
    | CaseB

type SecondType =
    | CaseA of string
    | CaseB
    | CaseC of FirstType

type ThirdType =
    | First of FirstType
    | Second of SecondType
    | Fourth of FourthType

and FourthType =
    | CaseFooBar of Foo * Bar
    | CaseFun of (Foo -> Bar)

and Foo = Foo
and Bar = Bar

type RecursiveType =
    | Other of string
    | Self of RecursiveType

