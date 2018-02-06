namespace Bricelam.EntityFrameworkCore.FSharp

open Microsoft.EntityFrameworkCore.Internal

module internal IndentedStringBuilderUtilities =

    let append (text:string) (sb:IndentedStringBuilder) =
        sb.Append(text)

    let appendLine (text:string) (sb:IndentedStringBuilder) =
        sb.AppendLine(text)

    let indent (sb:IndentedStringBuilder) =
        sb.IncrementIndent()

    let unindent (sb:IndentedStringBuilder) =
        sb.DecrementIndent()

    let writeNamespaces namespaces (sb:IndentedStringBuilder) =
        namespaces
            |> Seq.iter(fun n -> sb.AppendLine("open " + n) |> ignore)
        sb        
