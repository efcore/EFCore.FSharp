namespace EntityFrameworkCore.FSharp

[<AutoOpen>]
module StringBuffer =
    open System
    open Microsoft.EntityFrameworkCore.Infrastructure

    type StringBuffer = IndentedStringBuilder -> unit

    let private writeStringBuffer (f: StringBuffer) (indent: bool) =
        let b = IndentedStringBuilder()

        if indent then
            b.IncrementIndent() |> ignore

        do f b

        if indent then
            b.DecrementIndent() |> ignore

        b.ToString()

    let notNull o = o |> isNull |> not

    let join (separator: string) (strings: string seq) =
        if Seq.isEmpty strings then
            String.Empty
        else
            strings
            |> Seq.reduce (fun x y -> x + separator + y)

    let ifTrue condition txt = if condition then Some txt else None

    let writeNamespaces (namespaces: string seq) =
        namespaces
        |> Seq.map (fun n -> "open " + n)
        |> join Environment.NewLine

    type StringBufferBuilder() =

        member inline __.Yield(txt: string) =
            fun (b: IndentedStringBuilder) -> Printf.kprintf (b.AppendLines >> ignore) "%s" txt

        member inline __.Yield(c: char) =
            fun (b: IndentedStringBuilder) -> Printf.kprintf (b.AppendLine >> ignore) "%c" c

        member inline __.YieldFrom(f: StringBuffer) = f

        member __.Combine(f, g) =
            fun (b: IndentedStringBuilder) ->
                f b
                g b

        member __.Delay f =
            fun (b: IndentedStringBuilder) -> (f ()) b

        member __.Zero() = ignore

        member __.For(xs: 'a seq, f: 'a -> StringBuffer) =
            fun (b: IndentedStringBuilder) ->
                use e = xs.GetEnumerator()

                while e.MoveNext() do
                    (f e.Current) b

        member __.While(p: unit -> bool, f: StringBuffer) =
            fun (b: IndentedStringBuilder) ->
                while p () do
                    f b

        abstract member Run: StringBuffer -> string
        default this.Run(f: StringBuffer) = writeStringBuffer f false

        member inline _.Yield(lines: string seq) =
            fun (b: IndentedStringBuilder) ->
                for txt in lines do
                    Printf.kprintf (b.AppendLines >> ignore) "%s" txt

        member inline __.Yield(txt: string option) =
            fun (b: IndentedStringBuilder) ->
                match txt with
                | Some t -> Printf.kprintf (b.AppendLines >> ignore) "%s" t
                | None -> ()

        member inline __.Yield(txt: string option seq) =
            fun (b: IndentedStringBuilder) ->
                for tt in txt do
                    match tt with
                    | Some t -> Printf.kprintf (b.AppendLines >> ignore) "%s" t
                    | None -> ()

    type IndentStringBufferBuilder() =
        inherit StringBufferBuilder()
        with

            override _.Run(f: StringBuffer) = writeStringBuffer f true

    let stringBuilder = new StringBufferBuilder()
    let indent = new IndentStringBufferBuilder()
