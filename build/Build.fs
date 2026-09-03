// ========================================================================================================
// === F# / Project fake build ==================================================================== 1.6.0 =
// --------------------------------------------------------------------------------------------------------
// Options:
//  - no-clean   - disables clean of dirs in the first step (required on CI)
//  - no-lint    - lint will be executed, but the result is not validated
// ========================================================================================================

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

open ProjectBuild
open Utils

[<EntryPoint>]
let main args =
    args |> Args.init

    Targets.init {
        Project = {
            Name = "TUC.Console"
            Summary = "Console application for .tuc commands."
            Git = Git.init ()
        }
        Specs =
            Spec.defaultConsoleApplication [
                OSX
                Windows
                Linux
            ]
    }

    Target.create "PlantUml" (fun _ ->
        match PlantUml.ensure () with
        | PlantUml.Verified version -> Trace.tracefn "[PlantUML] Using verified v%s JAR." version
        | PlantUml.Downloaded version -> Trace.tracefn "[PlantUML] Downloaded and verified v%s JAR." version
    )

    "PlantUml" ==> "Build" |> ignore

    args |> Args.run
