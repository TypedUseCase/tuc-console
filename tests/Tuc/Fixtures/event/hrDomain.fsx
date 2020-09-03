#load "../CoreTypes.fsx"
open CoreTypes

type GenericService = Initiator

type WorkGroupEvent =
    | WorkGroupMemberEvent
    | WorkGroupHierarchyEvent

type WorkGroupFactEvent =
    | WorkGroupEvent of WorkGroupEvent

type WorkGroupFactStream = WorkGroupFactStream of Stream<WorkGroupFactEvent>
