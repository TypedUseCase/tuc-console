(*
Entities [1]
------------

----------- ------------ -------------
 FullName    AccessPath   DisplayName
----------- ------------ -------------
 "Service"   "global"     "Service"
----------- ------------ -------------

Service.Entities [4]
-------------------- *)

type Service = {
    FirstMethod: SingleAttribute -> MethodResult
    SecondMethod: SingleAttribute -> Name -> MethodResult
    ThirdMethod: SingleAttribute * Name -> unit
}

(*
------------------- ------------ -------------
 FullName            AccessPath   DisplayName
------------------- ------------ -------------
 "Service.Service"   "Service"    "Service"
------------------- ------------ -------------

Fields [3]
----------

----------------------------- ------------- ---------------------------------
 FullName                      DisplayName   FieldType
----------------------------- ------------- ---------------------------------
 Service.Service.FirstMethod   FirstMethod   SingleAttribute -> MethodResult
----------------------------- ------------- ---------------------------------

------------------------------ -------------- -----------------------------------------
 FullName                       DisplayName    FieldType
------------------------------ -------------- -----------------------------------------
 Service.Service.SecondMethod   SecondMethod   SingleAttribute -> Name -> MethodResult
------------------------------ -------------- -----------------------------------------

----------------------------- ------------- --------------------------------
 FullName                      DisplayName   FieldType
----------------------------- ------------- --------------------------------
 Service.Service.ThirdMethod   ThirdMethod   SingleAttribute * Name -> unit
----------------------------- ------------- -------------------------------- *)

and Name = Name of string

(*
---------------- ------------ -------------
 FullName         AccessPath   DisplayName
---------------- ------------ -------------
 "Service.Name"   "Service"    "Name"
---------------- ------------ -------------

e.UnionCases [1]
----------------

------------------- ------------- ----------- ---------
 FullName            DisplayName   HasFields   Returns
------------------- ------------- ----------- ---------
 Service.Name.Name   Name          true        Name
------------------- ------------- ----------- ---------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)

and SingleAttribute = SingleAttribute of string

(*
--------------------------- ------------ -------------------
 FullName                    AccessPath   DisplayName
--------------------------- ------------ -------------------
 "Service.SingleAttribute"   "Service"    "SingleAttribute"
--------------------------- ------------ -------------------

e.UnionCases [1]
----------------

----------------------------------------- ----------------- ----------- -----------------
 FullName                                  DisplayName       HasFields   Returns
----------------------------------------- ----------------- ----------- -----------------
 Service.SingleAttribute.SingleAttribute   SingleAttribute   true        SingleAttribute
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
 "Service.MethodResult"   "Service"    "MethodResult"
------------------------ ------------ ----------------

e.UnionCases [2]
----------------

------------------------- ------------- ----------- --------------
 FullName                  DisplayName   HasFields   Returns
------------------------- ------------- ----------- --------------
 Service.MethodResult.Ok   Ok            false       MethodResult
------------------------- ------------- ----------- --------------

---------------------------- ------------- ----------- --------------
 FullName                     DisplayName   HasFields   Returns
---------------------------- ------------- ----------- --------------
 Service.MethodResult.Error   Error         false       MethodResult
---------------------------- ------------- ----------- -------------- *)
