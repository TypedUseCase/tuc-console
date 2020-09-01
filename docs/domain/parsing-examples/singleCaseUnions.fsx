(*
Entities [1]
------------

-------------------- ------------ --------------------
 FullName             AccessPath   DisplayName
-------------------- ------------ --------------------
 "SingleCaseUnions"   "global"     "SingleCaseUnions"
-------------------- ------------ --------------------

SingleCaseUnions.Entities [4]
----------------------------- *)

type FirstType = FirstType

(*
------------------------------ -------------------- -------------
 FullName                       AccessPath           DisplayName
------------------------------ -------------------- -------------
 "SingleCaseUnions.FirstType"   "SingleCaseUnions"   "FirstType"
------------------------------ -------------------- -------------

e.UnionCases [1]
----------------

-------------------------------------- ------------- ----------- -----------
 FullName                               DisplayName   HasFields   Returns
-------------------------------------- ------------- ----------- -----------
 SingleCaseUnions.FirstType.FirstType   FirstType     false       FirstType
-------------------------------------- ------------- ----------- ----------- *)

type SecondType = SecondTypeCtr of string

(*
------------------------------- -------------------- --------------
 FullName                        AccessPath           DisplayName
------------------------------- -------------------- --------------
 "SingleCaseUnions.SecondType"   "SingleCaseUnions"   "SecondType"
------------------------------- -------------------- --------------

e.UnionCases [1]
----------------

------------------------------------------- --------------- ----------- ------------
 FullName                                    DisplayName     HasFields   Returns
------------------------------------------- --------------- ----------- ------------
 SingleCaseUnions.SecondType.SecondTypeCtr   SecondTypeCtr   true        SecondType
------------------------------------------- --------------- ----------- ------------

Fields [1]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item       Item          string
---------- ------------- ----------- *)

type ThirdType = ThirdType of first: string * second: string

(*
------------------------------ -------------------- -------------
 FullName                       AccessPath           DisplayName
------------------------------ -------------------- -------------
 "SingleCaseUnions.ThirdType"   "SingleCaseUnions"   "ThirdType"
------------------------------ -------------------- -------------

e.UnionCases [1]
----------------

-------------------------------------- ------------- ----------- -----------
 FullName                               DisplayName   HasFields   Returns
-------------------------------------- ------------- ----------- -----------
 SingleCaseUnions.ThirdType.ThirdType   ThirdType     true        ThirdType
-------------------------------------- ------------- ----------- -----------

Fields [2]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 first      first         string
---------- ------------- -----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 second     second        string
---------- ------------- ----------- *)

type FourthType = FourthType of string * int

(*
------------------------------- -------------------- --------------
 FullName                        AccessPath           DisplayName
------------------------------- -------------------- --------------
 "SingleCaseUnions.FourthType"   "SingleCaseUnions"   "FourthType"
------------------------------- -------------------- --------------

e.UnionCases [1]
----------------

---------------------------------------- ------------- ----------- ------------
 FullName                                 DisplayName   HasFields   Returns
---------------------------------------- ------------- ----------- ------------
 SingleCaseUnions.FourthType.FourthType   FourthType    true        FourthType
---------------------------------------- ------------- ----------- ------------

Fields [2]
----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item1      Item1         string
---------- ------------- -----------

---------- ------------- -----------
 FullName   DisplayName   FieldType
---------- ------------- -----------
 Item2      Item2         int
---------- ------------- ----------- *)
