(*
Entities [1]
------------

------------ ------------ -------------
 FullName     AccessPath   DisplayName
------------ ------------ -------------
 "Generics"   "global"     "Generics"
------------ ------------ -------------

Generics.Entities [8]
--------------------- *)

type Stream<'Event> = Stream of 'Event list

(*
--------------------- ------------ -------------
 FullName              AccessPath   DisplayName
--------------------- ------------ -------------
 "Generics.Stream`1"   "Generics"   "Stream"
--------------------- ------------ -------------

e.UnionCases [1]
----------------

--------------------------- ------------- ----------- ----------------
 FullName                    DisplayName   HasFields   Returns
--------------------------- ------------- ----------- ----------------
 Generics.Stream<_>.Stream   Stream        true        Stream<'Event>
--------------------------- ------------- ----------- ----------------

Fields [1]
----------

---------- ------------- -------------
 FullName   DisplayName   FieldType
---------- ------------- -------------
 Item       Item          'Event list
---------- ------------- ------------- *)

type StreamHandler<'Event> = StreamHandler of ('Event -> unit)

(*
---------------------------- ------------ -----------------
 FullName                     AccessPath   DisplayName
---------------------------- ------------ -----------------
 "Generics.StreamHandler`1"   "Generics"   "StreamHandler"
---------------------------- ------------ -----------------

e.UnionCases [1]
----------------

----------------------------------------- --------------- ----------- -----------------------
 FullName                                  DisplayName     HasFields   Returns
----------------------------------------- --------------- ----------- -----------------------
 Generics.StreamHandler<_>.StreamHandler   StreamHandler   true        StreamHandler<'Event>
----------------------------------------- --------------- ----------- -----------------------

Fields [1]
----------

---------- ------------- ----------------
 FullName   DisplayName   FieldType
---------- ------------- ----------------
 Item       Item          'Event -> unit
---------- ------------- ---------------- *)

type InteractionEvent =
    | Confirmation
    | Rejection

(*
----------------------------- ------------ --------------------
 FullName                      AccessPath   DisplayName
----------------------------- ------------ --------------------
 "Generics.InteractionEvent"   "Generics"   "InteractionEvent"
----------------------------- ------------ --------------------

e.UnionCases [2]
----------------

---------------------------------------- -------------- ----------- ------------------
 FullName                                 DisplayName    HasFields   Returns
---------------------------------------- -------------- ----------- ------------------
 Generics.InteractionEvent.Confirmation   Confirmation   false       InteractionEvent
---------------------------------------- -------------- ----------- ------------------

------------------------------------- ------------- ----------- ------------------
 FullName                              DisplayName   HasFields   Returns
------------------------------------- ------------- ----------- ------------------
 Generics.InteractionEvent.Rejection   Rejection     false       InteractionEvent
------------------------------------- ------------- ----------- ------------------ *)

type InteractionCollectorStream = InteractionCollectorStream of Stream<InteractionEvent>

(*
--------------------------------------- ------------ ------------------------------
 FullName                                AccessPath   DisplayName
--------------------------------------- ------------ ------------------------------
 "Generics.InteractionCollectorStream"   "Generics"   "InteractionCollectorStream"
--------------------------------------- ------------ ------------------------------

e.UnionCases [1]
----------------

---------------------------------------------------------------- ---------------------------- ----------- ----------------------------
 FullName                                                         DisplayName                  HasFields   Returns
---------------------------------------------------------------- ---------------------------- ----------- ----------------------------
 Generics.InteractionCollectorStream.InteractionCollectorStream   InteractionCollectorStream   true        InteractionCollectorStream
---------------------------------------------------------------- ---------------------------- ----------- ----------------------------

Fields [1]
----------

---------- ------------- --------------------------
 FullName   DisplayName   FieldType
---------- ------------- --------------------------
 Item       Item          Stream<InteractionEvent>
---------- ------------- -------------------------- *)

type PersonIdentificationEngine = {
    OnInteractionEvent: StreamHandler<InteractionEvent>
}

(*
--------------------------------------- ------------ ------------------------------
 FullName                                AccessPath   DisplayName
--------------------------------------- ------------ ------------------------------
 "Generics.PersonIdentificationEngine"   "Generics"   "PersonIdentificationEngine"
--------------------------------------- ------------ ------------------------------

Fields [1]
----------

-------------------------------------------------------- -------------------- ---------------------------------
 FullName                                                 DisplayName          FieldType
-------------------------------------------------------- -------------------- ---------------------------------
 Generics.PersonIdentificationEngine.OnInteractionEvent   OnInteractionEvent   StreamHandler<InteractionEvent>
-------------------------------------------------------- -------------------- --------------------------------- *)

type Method = Method of ((Input list) option -> Async<Result<Output option, string>> list)

(*
------------------- ------------ -------------
 FullName            AccessPath   DisplayName
------------------- ------------ -------------
 "Generics.Method"   "Generics"   "Method"
------------------- ------------ -------------

e.UnionCases [1]
----------------

------------------------ ------------- ----------- ---------
 FullName                 DisplayName   HasFields   Returns
------------------------ ------------- ----------- ---------
 Generics.Method.Method   Method        true        Method
------------------------ ------------- ----------- ---------

Fields [1]
----------

---------- ------------- ----------------------------------------------------------------
 FullName   DisplayName   FieldType
---------- ------------- ----------------------------------------------------------------
 Item       Item          Input list option -> Async<Result<Output option, string>> list
---------- ------------- ---------------------------------------------------------------- *)

and Input = Input of string

(*
------------------ ------------ -------------
 FullName           AccessPath   DisplayName
------------------ ------------ -------------
 "Generics.Input"   "Generics"   "Input"
------------------ ------------ -------------

e.UnionCases [1]
----------------

---------------------- ------------- ----------- ---------
 FullName               DisplayName   HasFields   Returns
---------------------- ------------- ----------- ---------
 Generics.Input.Input   Input         true        Input
---------------------- ------------- ----------- ---------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)

and Output = Output of string

(*
------------------- ------------ -------------
 FullName            AccessPath   DisplayName
------------------- ------------ -------------
 "Generics.Output"   "Generics"   "Output"
------------------- ------------ -------------

e.UnionCases [1]
----------------

------------------------ ------------- ----------- ---------
 FullName                 DisplayName   HasFields   Returns
------------------------ ------------- ----------- ---------
 Generics.Output.Output   Output        true        Output
------------------------ ------------- ----------- ---------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)
