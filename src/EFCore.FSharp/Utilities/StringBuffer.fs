namespace EntityFrameworkCore.FSharp

[<AutoOpen>]
module StringBuffer =
    open System
    open Microsoft.EntityFrameworkCore.Infrastructure

    type StringBuffer = IndentedStringBuilder -> unit

    type private IndentationLevel =
        | NoIndent
        | Indent

    let private writeStringBuffer (f: StringBuffer) (indent: IndentationLevel) =
        let b = IndentedStringBuilder()

        match indent with
        | NoIndent -> ()
        | Indent -> b.IncrementIndent() |> ignore

        do f b

        match indent with
        | NoIndent -> ()
        | Indent -> b.DecrementIndent() |> ignore

        b.ToString()

    let notNull o = o |> isNull |> not
    let (?=) (a: #obj) (b: #obj) = if isNull a then b else a

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

    let unindent (n: int) (txt: string option) =
        fun (b: IndentedStringBuilder) ->
            match txt with
            | Some t ->
                for _ in [ 1 .. n ] do
                    b.DecrementIndent() |> ignore

                Printf.kprintf (b.AppendLines >> ignore) "%s" t

                for _ in [ 1 .. n ] do
                    b.IncrementIndent() |> ignore
            | None -> ()

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
        default this.Run(f: StringBuffer) = writeStringBuffer f NoIndent

        member inline _.Yield(lines: string seq) =
            fun (b: IndentedStringBuilder) ->
                lines
                |> Seq.iter (fun txt -> Printf.kprintf (b.AppendLines >> ignore) "%s" txt)

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

            override _.Run(f: StringBuffer) = writeStringBuffer f Indent

    let stringBuilder = new StringBufferBuilder()
    let indent = new IndentStringBufferBuilder()
