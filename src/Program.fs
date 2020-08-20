open System
open System.IO
open MF.ConsoleApplication
open MF.TucConsole
open MF.TucConsole.Console
open MF.Domain
open ErrorHandling

[<EntryPoint>]
let main argv =
    consoleApplication {
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion

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
                Argument.tucFile
            ]
            Options = [
                Option.optional "output" (Some "o") "Path to an output file for generated PlantUML. (It must be a .puml)" None
                Option.optional "image" (Some "i") "Path to an output image file for generated PlantUML. (It must be a .png)" None
                Option.optional "tuc" (Some "t") "Tuc name, which should only be generated (from multi-tuc file)." None
                Option.noValue "watch" (Some "w") "Whether to watch a tuc file, to change an output file on the fly."
            ]
            Initialize = None
            Interact = None
            Execute = Command.Tuc.generate
        }

        command "about" {
            Description = "Displays information about the current project."
            Help = None
            Arguments = []
            Options = []
            Initialize = None
            Interact = None
            Execute = Command.Common.about
        }
    }
    |> run argv
