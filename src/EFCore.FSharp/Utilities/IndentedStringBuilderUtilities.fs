namespace EntityFrameworkCore.FSharp

open System
open Microsoft.EntityFrameworkCore.Infrastructure

module internal IndentedStringBuilderUtilities =

    let notNull o = o |> isNull |> not
    let (?=) (a: #obj) (b: #obj) = if isNull a then b else a

    let join (separator : string) (strings : string seq) =
        if Seq.isEmpty strings then
            String.Empty
        else
            strings |> Seq.reduce (fun x y -> x + separator + y)

    let append (text:string) (sb:IndentedStringBuilder) =
        sb.Append(text)

    let appendLine (text:string) (sb:IndentedStringBuilder) =
        sb.AppendLine(text)

    let appendEmptyLine sb =
        sb |> appendLine String.Empty

    let appendIfTrue truth value b =
        if truth then
            b |> append value
        else
            b

    let appendLineIfTrue truth value b =
        if truth then
            b |> appendEmptyLine |> append value
        else
            b

    let private prependLine (addLineBreak: bool ref) (text:string) (sb:IndentedStringBuilder) =
        if addLineBreak.Value then
            sb |> appendEmptyLine |> ignore
        else
            addLineBreak := true

        sb |> append text |> ignore

    let appendLines (lines: string seq) skipFinalNewLine (sb:IndentedStringBuilder) =

        let addLineBreak = ref false

        lines |> Seq.iter(fun l -> sb |> prependLine addLineBreak l)

        if skipFinalNewLine then
            sb
        else
            sb |> appendEmptyLine

    let appendLineIndent message (sb:IndentedStringBuilder) =
        using (sb.Indent())
            (fun _ -> sb.AppendLine(message))

    let indent (sb:IndentedStringBuilder) =
        sb.IncrementIndent()

    let unindent (sb:IndentedStringBuilder) =
        sb.DecrementIndent()

    let writeNamespaces namespaces (sb:IndentedStringBuilder) =
        namespaces
            |> Seq.iter(fun n -> sb |> appendLine ("open " + n) |> ignore)
        sb

    let appendAutoGeneratedTag (sb:IndentedStringBuilder) =
        sb |> appendLine "// <auto-generated />"

