#load "shared.fsx"
open CoreTypes
open Shared

type MainService = Initiator

type Sender = {
    SendDto: Dto -> CommandResult
}
