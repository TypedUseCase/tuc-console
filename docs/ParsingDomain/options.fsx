(*
Entities [1]
------------

----------- ------------ -------------
 FullName    AccessPath   DisplayName
----------- ------------ -------------
 "Options"   "global"     "Options"
----------- ------------ -------------

Options.Entities [1]
-------------------- *)

type MaybeName = MaybeName of string option

(*
--------------------- ------------ -------------
 FullName              AccessPath   DisplayName
--------------------- ------------ -------------
 "Options.MaybeName"   "Options"    "MaybeName"
--------------------- ------------ -------------

e.UnionCases [1]
----------------

----------------------------- ------------- ----------- -----------
 FullName                      DisplayName   HasFields   Returns
----------------------------- ------------- ----------- -----------
 Options.MaybeName.MaybeName   MaybeName     true        MaybeName
----------------------------- ------------- ----------- -----------

Fields [1]
----------

---------- ------------- ---------------
 FullName   DisplayName   FieldType
---------- ------------- ---------------
 Item       Item          string option
---------- ------------- --------------- *)
