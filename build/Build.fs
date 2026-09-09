open Alma.Build
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

open ProjectBuild
open Utils

[<EntryPoint>]
let main args =
    args |> Args.init

    let runtimeTargets = [
        OSXArm64
        Windows
        Linux
    ]

    let spec =
        Spec.defaultConsoleApplication runtimeTargets
        |> Spec.mapConsoleApplication (fun spec -> {
            spec with
                RuntimeMode = RuntimeMode.AutoDetect
                PublishSingleFile = false
        })

    Targets.init {
        Project = {
            Name = "TUC.Console"
            Summary = "Console application for .tuc commands."
            Git = Git.init ()
        }
        Specs = spec
    }

    PlantUml.init runtimeTargets

    args |> Args.run
