
(*
Entities [1]
------------

------------- ------------ -------------
 FullName      AccessPath   DisplayName
------------- ------------ -------------
 "Functions"   "global"     "Functions"
------------- ------------ -------------

Functions.Entities [4]
---------------------- *)

type Function = Function of (string -> string)

(*
---------------------- ------------- -------------
 FullName               AccessPath    DisplayName
---------------------- ------------- -------------
 "Functions.Function"   "Functions"   "Function"
---------------------- ------------- -------------

e.UnionCases [1]
----------------

----------------------------- ------------- ----------- ----------
 FullName                      DisplayName   HasFields   Returns
----------------------------- ------------- ----------- ----------
 Functions.Function.Function   Function      true        Function
----------------------------- ------------- ----------- ----------

Fields [1]
----------

---------- ------------- ------------------
 FullName   DisplayName   FieldType
---------- ------------- ------------------
 Item       Item          string -> string
---------- ------------- ------------------ *)

type SecondFunction = SecondFunction of (int * string -> unit -> string)

(*
---------------------------- ------------- ------------------
 FullName                     AccessPath    DisplayName
---------------------------- ------------- ------------------
 "Functions.SecondFunction"   "Functions"   "SecondFunction"
---------------------------- ------------- ------------------

e.UnionCases [1]
----------------

----------------------------------------- ---------------- ----------- ----------------
 FullName                                  DisplayName      HasFields   Returns
----------------------------------------- ---------------- ----------- ----------------
 Functions.SecondFunction.SecondFunction   SecondFunction   true        SecondFunction
----------------------------------------- ---------------- ----------- ----------------

Fields [1]
----------

---------- ------------- --------------------------------
 FullName   DisplayName   FieldType
---------- ------------- --------------------------------
 Item       Item          int * string -> unit -> string
---------- ------------- -------------------------------- *)

type GenericFunction<'Input, 'Output> = GenericFunction of (string -> 'Input -> 'Output)

(*
------------------------------- ------------- -------------------
 FullName                        AccessPath    DisplayName
------------------------------- ------------- -------------------
 "Functions.GenericFunction`2"   "Functions"   "GenericFunction"
------------------------------- ------------- -------------------

e.UnionCases [1]
----------------

------------------------------------------------ ----------------- ----------- ----------------------------------
 FullName                                         DisplayName       HasFields   Returns
------------------------------------------------ ----------------- ----------- ----------------------------------
 Functions.GenericFunction<_,_>.GenericFunction   GenericFunction   true        GenericFunction<'Input, 'Output>
------------------------------------------------ ----------------- ----------- ----------------------------------

Fields [1]
----------

---------- ------------- -----------------------------
 FullName   DisplayName   FieldType
---------- ------------- -----------------------------
 Item       Item          string -> 'Input -> 'Output
---------- ------------- ----------------------------- *)

type ComplexFunction<'Input> = ComplexFunction of ('Input list -> string option -> Async<Result<'Input, string>>)

(*
------------------------------- ------------- -------------------
 FullName                        AccessPath    DisplayName
------------------------------- ------------- -------------------
 "Functions.ComplexFunction`1"   "Functions"   "ComplexFunction"
------------------------------- ------------- -------------------

e.UnionCases [1]
----------------

---------------------------------------------- ----------------- ----------- -------------------------
 FullName                                       DisplayName       HasFields   Returns
---------------------------------------------- ----------------- ----------- -------------------------
 Functions.ComplexFunction<_>.ComplexFunction   ComplexFunction   true        ComplexFunction<'Input>
---------------------------------------------- ----------------- ----------- -------------------------

Fields [1]
----------

---------- ------------- ---------------------------------------------------------------
 FullName   DisplayName   FieldType
---------- ------------- ---------------------------------------------------------------
 Item       Item          'Input list -> string option -> Async<Result<'Input, string>>
---------- ------------- --------------------------------------------------------------- *)
