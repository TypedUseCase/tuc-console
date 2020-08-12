(*
Entities [1]
------------

----------------------- ------------ -----------------------
 FullName                AccessPath   DisplayName
----------------------- ------------ -----------------------
 "DiscriminatedUnions"   "global"     "DiscriminatedUnions"
----------------------- ------------ -----------------------

DiscriminatedUnions.Entities [3]
-------------------------------- *)

type FirstType =
    | CaseA
    | CaseB

(*
--------------------------------- ----------------------- -------------
 FullName                          AccessPath              DisplayName
--------------------------------- ----------------------- -------------
 "DiscriminatedUnions.FirstType"   "DiscriminatedUnions"   "FirstType"
--------------------------------- ----------------------- -------------

e.UnionCases [2]
----------------

------------------------------------- ------------- ----------- -----------
 FullName                              DisplayName   HasFields   Returns
------------------------------------- ------------- ----------- -----------
 DiscriminatedUnions.FirstType.CaseA   CaseA         false       FirstType
------------------------------------- ------------- ----------- -----------

------------------------------------- ------------- ----------- -----------
 FullName                              DisplayName   HasFields   Returns
------------------------------------- ------------- ----------- -----------
 DiscriminatedUnions.FirstType.CaseB   CaseB         false       FirstType
------------------------------------- ------------- ----------- ----------- *)

type SecondType =
    | CaseA of string
    | CaseB
    | CaseC of FirstType

(*
---------------------------------- ----------------------- --------------
 FullName                           AccessPath              DisplayName
---------------------------------- ----------------------- --------------
 "DiscriminatedUnions.SecondType"   "DiscriminatedUnions"   "SecondType"
---------------------------------- ----------------------- --------------

e.UnionCases [3]
----------------

-------------------------------------- ------------- ----------- ------------
 FullName                               DisplayName   HasFields   Returns
-------------------------------------- ------------- ----------- ------------
 DiscriminatedUnions.SecondType.CaseA   CaseA         true        SecondType
-------------------------------------- ------------- ----------- ------------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- -----------

-------------------------------------- ------------- ----------- ------------
 FullName                               DisplayName   HasFields   Returns
-------------------------------------- ------------- ----------- ------------
 DiscriminatedUnions.SecondType.CaseB   CaseB         false       SecondType
-------------------------------------- ------------- ----------- ------------

-------------------------------------- ------------- ----------- ------------
 FullName                               DisplayName   HasFields   Returns
-------------------------------------- ------------- ----------- ------------
 DiscriminatedUnions.SecondType.CaseC   CaseC         true        SecondType
-------------------------------------- ------------- ----------- ------------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          FirstType
---------- ------------- ----------- *)

type RecursiveType =
    | Other of string
    | Self of RecursiveType

(*
------------------------------------- ----------------------- -----------------
 FullName                              AccessPath              DisplayName
------------------------------------- ----------------------- -----------------
 "DiscriminatedUnions.RecursiveType"   "DiscriminatedUnions"   "RecursiveType"
------------------------------------- ----------------------- -----------------

e.UnionCases [2]
----------------

----------------------------------------- ------------- ----------- ---------------
 FullName                                  DisplayName   HasFields   Returns
----------------------------------------- ------------- ----------- ---------------
 DiscriminatedUnions.RecursiveType.Other   Other         true        RecursiveType
----------------------------------------- ------------- ----------- ---------------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- -----------

---------------------------------------- ------------- ----------- ---------------
 FullName                                 DisplayName   HasFields   Returns
---------------------------------------- ------------- ----------- ---------------
 DiscriminatedUnions.RecursiveType.Self   Self          true        RecursiveType
---------------------------------------- ------------- ----------- ---------------

Fields [1]
----------

---------- ------------- ---------------
 FullName   DisplayName   FieldType
---------- ------------- ---------------
 Item       Item          RecursiveType
---------- ------------- --------------- *)
