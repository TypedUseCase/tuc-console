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
                Option.noValue "only-parse" (Some "p") "Whether to just parse domain and dump a results."
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
                Argument.tuc
            ]
            Options = [
                Option.noValue "watch" (Some "w") "Whether to watch domain file(s) and tuc file for changes."
            ]
            Initialize = None
            Interact = None
            Execute = Command.Tuc.check
        }

        command "tuc:generate" {
            Description = ""
            Help = None
            Arguments = [
                Argument.required "tuc" "Path to tuc file containing a Typed Use-Case definition."
                Argument.domain
            ]
            Options = [
                Option.optional "output" (Some "o") "Path to an output PlantUML file. (If not set, it will be a input file name with .puml" None
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
