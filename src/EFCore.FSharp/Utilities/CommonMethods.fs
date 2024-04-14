[<AutoOpen>]
module CommonMethods

open System

let notNull o =
    o
    |> isNull
    |> not

let (?=) (a: #obj) (b: #obj) = if isNull a then b else a

let join (separator: string) (strings: string seq) =
    if Seq.isEmpty strings then
        String.Empty
    else
        strings
        |> Seq.reduce (fun x y ->
            x
            + separator
            + y
        )
