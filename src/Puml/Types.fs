namespace MF.Puml

open Tuc.Domain
open MF.Tuc

type Puml = Puml of string
type PumlImage = PumlImage of byte []

[<RequireQualifiedAccess>]
module Puml =
    let value (Puml puml) = puml

[<RequireQualifiedAccess>]
module PumlImage =
    let value (PumlImage pumlImage) = pumlImage
