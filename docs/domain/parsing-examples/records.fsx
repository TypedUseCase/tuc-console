(*
Entities [1]
------------

----------- ------------ -------------
 FullName    AccessPath   DisplayName
----------- ------------ -------------
 "Records"   "global"     "Records"
----------- ------------ -------------

Records.Entities [7]
-------------------- *)

type Name = {
    FirstName: string
    Middle: string option
    LastName: string
}

(*
---------------- ------------ -------------
 FullName         AccessPath   DisplayName
---------------- ------------ -------------
 "Records.Name"   "Records"    "Name"
---------------- ------------ -------------

Fields [3]
----------

------------------------ ------------- -----------
 FullName                 DisplayName   FieldType
------------------------ ------------- -----------
 Records.Name.FirstName   FirstName     string
------------------------ ------------- -----------

--------------------- ------------- ---------------
 FullName              DisplayName   FieldType
--------------------- ------------- ---------------
 Records.Name.Middle   Middle        string option
--------------------- ------------- ---------------

----------------------- ------------- -----------
 FullName                DisplayName   FieldType
----------------------- ------------- -----------
 Records.Name.LastName   LastName      string
----------------------- ------------- ----------- *)

type ContractCreatedEvent = {
    ContractId: System.Guid
    Intent: Intent
    Texts: Text list
}

(*
-------------------------------- ------------ ------------------------
 FullName                         AccessPath   DisplayName
-------------------------------- ------------ ------------------------
 "Records.ContractCreatedEvent"   "Records"    "ContractCreatedEvent"
-------------------------------- ------------ ------------------------

Fields [3]
----------

----------------------------------------- ------------- -----------
 FullName                                  DisplayName   FieldType
----------------------------------------- ------------- -----------
 Records.ContractCreatedEvent.ContractId   ContractId    Guid
----------------------------------------- ------------- -----------

------------------------------------- ------------- -----------
 FullName                              DisplayName   FieldType
------------------------------------- ------------- -----------
 Records.ContractCreatedEvent.Intent   Intent        Intent
------------------------------------- ------------- -----------

------------------------------------ ------------- -----------
 FullName                             DisplayName   FieldType
------------------------------------ ------------- -----------
 Records.ContractCreatedEvent.Texts   Texts         Text list
------------------------------------ ------------- ----------- *)

and Intent = {
    Purpose: string
    Scope: string
}

(*
------------------ ------------ -------------
 FullName           AccessPath   DisplayName
------------------ ------------ -------------
 "Records.Intent"   "Records"    "Intent"
------------------ ------------ -------------

Fields [2]
----------

------------------------ ------------- -----------
 FullName                 DisplayName   FieldType
------------------------ ------------- -----------
 Records.Intent.Purpose   Purpose       string
------------------------ ------------- -----------

---------------------- ------------- -----------
 FullName               DisplayName   FieldType
---------------------- ------------- -----------
 Records.Intent.Scope   Scope         string
---------------------- ------------- ----------- *)

and Text = Text of string

(*
---------------- ------------ -------------
 FullName         AccessPath   DisplayName
---------------- ------------ -------------
 "Records.Text"   "Records"    "Text"
---------------- ------------ -------------

e.UnionCases [1]
----------------

------------------- ------------- ----------- ---------
 FullName            DisplayName   HasFields   Returns
------------------- ------------- ----------- ---------
 Records.Text.Text   Text          true        Text
------------------- ------------- ----------- ---------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)

type Service = {
    FirstMethod: SingleAttribute -> MethodResult
    SecondMethod: SingleAttribute -> Name -> MethodResult
    ThirdMethod: SingleAttribute * Name -> unit
}

(*
------------------- ------------ -------------
 FullName            AccessPath   DisplayName
------------------- ------------ -------------
 "Records.Service"   "Records"    "Service"
------------------- ------------ -------------

Fields [3]
----------

----------------------------- ------------- ---------------------------------
 FullName                      DisplayName   FieldType
----------------------------- ------------- ---------------------------------
 Records.Service.FirstMethod   FirstMethod   SingleAttribute -> MethodResult
----------------------------- ------------- ---------------------------------

------------------------------ -------------- -----------------------------------------
 FullName                       DisplayName    FieldType
------------------------------ -------------- -----------------------------------------
 Records.Service.SecondMethod   SecondMethod   SingleAttribute -> Name -> MethodResult
------------------------------ -------------- -----------------------------------------

----------------------------- ------------- --------------------------------
 FullName                      DisplayName   FieldType
----------------------------- ------------- --------------------------------
 Records.Service.ThirdMethod   ThirdMethod   SingleAttribute * Name -> unit
----------------------------- ------------- -------------------------------- *)

and SingleAttribute = SingleAttribute of string

(*
--------------------------- ------------ -------------------
 FullName                    AccessPath   DisplayName
--------------------------- ------------ -------------------
 "Records.SingleAttribute"   "Records"    "SingleAttribute"
--------------------------- ------------ -------------------

e.UnionCases [1]
----------------

----------------------------------------- ----------------- ----------- -----------------
 FullName                                  DisplayName       HasFields   Returns
----------------------------------------- ----------------- ----------- -----------------
 Records.SingleAttribute.SingleAttribute   SingleAttribute   true        SingleAttribute
----------------------------------------- ----------------- ----------- -----------------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)

and MethodResult =
    | Ok
    | Error

(*
------------------------ ------------ ----------------
 FullName                 AccessPath   DisplayName
------------------------ ------------ ----------------
 "Records.MethodResult"   "Records"    "MethodResult"
------------------------ ------------ ----------------

e.UnionCases [2]
----------------

------------------------- ------------- ----------- --------------
 FullName                  DisplayName   HasFields   Returns
------------------------- ------------- ----------- --------------
 Records.MethodResult.Ok   Ok            false       MethodResult
------------------------- ------------- ----------- --------------

---------------------------- ------------- ----------- --------------
 FullName                     DisplayName   HasFields   Returns
---------------------------- ------------- ----------- --------------
 Records.MethodResult.Error   Error         false       MethodResult
---------------------------- ------------- ----------- -------------- *)
