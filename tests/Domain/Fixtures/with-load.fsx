#load "records.fsx"
open Records

type Contract = {
    Text: string
    Intent: Intent
}
