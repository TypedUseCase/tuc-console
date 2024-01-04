open System
open System.IO
open MF.ConsoleApplication
open Tuc.Console
open Tuc.Console.Console
open Tuc.Domain
open MF.ErrorHandling

[<EntryPoint>]
let main argv =
    consoleApplication {
        name AssemblyVersionInformation.AssemblyProduct
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion
        description AssemblyVersionInformation.AssemblyDescription

        gitBranch AssemblyVersionInformation.AssemblyMetadata_gitbranch
        gitCommit AssemblyVersionInformation.AssemblyMetadata_gitcommit

        command "domain:check" {
            Description = "Checks given domains."
            Help = None
            Arguments = [
                Argument.domain
            ]
            Options = [
                Option.noValue "only-resolved" (Some "r") "Whether to just show a resolved domain types."
                Option.noValue "count" (Some "c") "Whether to just show a count of results."
                Option.noValue "watch" (Some "w") "Whether to watch domain file(s) for changes."
            ]
            Initialize = None
            Interact = None
            Execute = Command.Domain.check
        }

        command "tuc:check" {
            Description = "Checks given tuc."
            Help = None
            Arguments = [
                Argument.domain
                Argument.tucFileOrDir
            ]
            Options = [
                Option.noValue "detail" (Some "d") "Whether to show detailed information about parsed Tuc file."
                Option.noValue "diagnostics" None "Whether to show diagnostics."
                Option.noValue "watch" (Some "w") "Whether to watch domain file(s) and tuc file for changes."
            ]
            Initialize = None
            Interact = None
            Execute = Command.Tuc.check
        }

        command "tuc:generate" {
            Description = "Compile a tuc with domain types and generates a use-case in the PlantUML format out of it."
            Help = None
            Arguments = [
                Argument.domain
                Argument.tucFileOrDir
            ]
            Options = [
                let imageOutputFormats =
                    Tuc.Puml.Generate.ImageFormat.formats
                    |> String.concat ", "

                Option.optional "output" (Some "o") "Path to an output file for generated PlantUML. (It must be a .puml)" None
                Option.optional "image" (Some "i") (sprintf "Path to an output image file for generated PlantUML. (It must be one of [ %s ])" imageOutputFormats) None
                Option.optional "style" (Some "s") "Path to a file containing a styles for resulting .puml (in .puml notation)." None
                Option.noValue "watch" (Some "w") "Whether to watch a domain and tuc file/dir, to change an output file on the fly."
                Option.optional "tuc" (Some "t") "Tuc name, which should only be generated (from multi-tuc file)." None
                Option.noValue "all" None "Whether to generate a dir with all multi-tucs (from multi-tuc file)."
                Option.noValue "diagnostics" None "Whether to show diagnostics."
            ]
            Initialize = None
            Interact = None
            Execute = Command.Tuc.generate
        }
    }
    |> run argv
