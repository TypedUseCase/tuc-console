#load "shared.fsx"
open CoreTypes
open Shared

type MainService = Initiator

type Receiver = {
    ReceiveDto: Dto -> CommandResult
}
