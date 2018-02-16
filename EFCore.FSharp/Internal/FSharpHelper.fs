namespace Bricelam.EntityFrameworkCore.FSharp.Internal

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Internal
open Microsoft.EntityFrameworkCore.Metadata.Internal
open System.Globalization
open Bricelam.EntityFrameworkCore.FSharp
open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

module FSharpHelper =

    let private _builtInTypes = new Dictionary<Type, string>()
    _builtInTypes.Add(typedefof<bool>, "bool")
    _builtInTypes.Add(typedefof<byte>, "byte")
    _builtInTypes.Add(typedefof<sbyte>, "sbyte")
    _builtInTypes.Add(typedefof<char>, "char")
    _builtInTypes.Add(typedefof<Int16>, "Int16")
    _builtInTypes.Add(typedefof<int>, "int")
    _builtInTypes.Add(typedefof<Int64>, "Int64")
    _builtInTypes.Add(typedefof<UInt16>, "UInt16")
    _builtInTypes.Add(typedefof<UInt32>, "UInt32")
    _builtInTypes.Add(typedefof<UInt64>, "UInt64")
    _builtInTypes.Add(typedefof<decimal>, "decimal")
    _builtInTypes.Add(typedefof<float>, "float")
    _builtInTypes.Add(typedefof<double>, "double")
    _builtInTypes.Add(typedefof<string>, "string")
    _builtInTypes.Add(typedefof<obj>, "obj")

    let private _keywords =
        [|
            "abstract";
            "and";
            "as";
            "asr";
            "assert";
            "atomic";
            "base";
            "begin";
            "break";
            "checked";
            "class";
            "component";
            "const";
            "constraint";
            "constructor";
            "continue";
            "default";
            "delegate";
            "do";
            "done";
            "downcast";
            "downto";
            "eager";
            "elif";
            "else if";
            "else";
            "end";
            "event";
            "exception";
            "extern";
            "external";
            "false";
            "finally";
            "fixed";
            "for";
            "fun";
            "function";
            "functor";
            "global";
            "if";
            "in";
            "include";
            "inherit";
            "inline";
            "interface";
            "internal";
            "land";
            "lazy";
            "let!";
            "let";
            "lor";
            "lsl";
            "lsr";
            "lxor";
            "match";
            "member";
            "method";
            "mixin";
            "mod";
            "module";
            "mutable";
            "namespace";
            "new";
            "not struct";
            "not";
            "null";
            "object";
            "of";
            "open";
            "or";
            "override";
            "parallel";
            "private";
            "process";
            "protected";
            "public";
            "pure";
            "rec";
            "return!";
            "return";
            "sealed";
            "select";
            "sig";
            "static";
            "struct";
            "tailcall";
            "then";
            "to";
            "trait";
            "true";
            "try";
            "type";
            "upcast";
            "use!";
            "use";
            "val";
            "virtual";
            "void";
            "volatile"
            "when";
            "while";
            "with";
            "yield!";
            "yield";   
          |]

    let test = [| [|2|] |]    

    let private isNullableType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        (not typeInfo.IsValueType)
        || typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Nullable<_>>

    let private isOptionType (t:Type) =
        let typeInfo = t.GetTypeInfo()
        (not typeInfo.IsValueType)
        || typeInfo.IsGenericType
        && typeInfo.GetGenericTypeDefinition() = typedefof<Option<_>>

    let private isOptionOrNullableType (t:Type) =
        (t |> isNullableType) || (t |> isOptionType)

    let private unwrapNullableType (t:Type) =
        match t |> isNullableType with    
        | true -> t |> Nullable.GetUnderlyingType
        | false -> t

    let private unwrapOptionType (t:Type) =
        match t |> isOptionType with    
        | true -> t.GenericTypeArguments.[0]
        | false -> t

    let rec Reference (t: Type) =

        match _builtInTypes.TryGetValue t with
        | true, value -> value
        | _ ->
            if t |> isNullableType then sprintf "Nullable<%s>" (t |> unwrapNullableType |> Reference)
            elif t |> isOptionType then sprintf "%s option" (t |> unwrapOptionType |> Reference)
            else
                let builder = StringBuilder()

                if t.IsArray then
                    builder.Append(Reference(t.GetElementType())).Append("[") |> ignore
                    (',', t.GetArrayRank()) |> String |> builder.Append |> ignore    
                    builder.Append("]") |> ignore
                    
                elif t.IsNested then
                    builder.Append(Reference(t.DeclaringType)).Append(".") |> ignore

                builder.Append(t.ShortDisplayName()) |> string

    let private ensureDecimalPlaces (number:string) =
        if number.IndexOf('.') >= 0 then number else number + ".0"

    type LiteralWriter =       

        static member Literal(value: Enum) = Reference(value.GetType()) + "." + (value |> string)

        static member UnknownLiteral (value: obj) =
            if isNull value then
                "null"
            else                        
                match value with
                | :? DBNull -> "null"
                | _ ->
                    match value with
                    | :? Enum as e -> LiteralWriter.Literal e
                    | :? bool as e -> LiteralWriter.Literal e
                    | :? byte as e -> LiteralWriter.Literal e
                    | :? (byte array) as e -> LiteralWriter.Literal e
                    | :? char as e -> LiteralWriter.Literal e
                    | :? DateTime as e -> LiteralWriter.Literal e
                    | :? DateTimeOffset as e -> LiteralWriter.Literal e
                    | :? decimal as e -> LiteralWriter.Literal e
                    | :? double as e -> LiteralWriter.Literal e
                    | :? float32 as e -> LiteralWriter.Literal e
                    | :? Guid as e -> LiteralWriter.Literal e
                    | :? int as e -> LiteralWriter.Literal e
                    | :? Int64 as e -> LiteralWriter.Literal e
                    | :? sbyte as e -> LiteralWriter.Literal e
                    | :? Int16 as e -> LiteralWriter.Literal e
                    | :? string as e -> LiteralWriter.Literal e
                    | :? TimeSpan as e -> LiteralWriter.Literal e
                    | :? UInt32 as e -> LiteralWriter.Literal e
                    | :? UInt64 as e -> LiteralWriter.Literal e
                    | :? UInt16 as e -> LiteralWriter.Literal e
                    | _ ->
                        let t = value.GetType()
                        let type' =
                            if t |> isNullableType then t |> unwrapNullableType
                            elif t |> isOptionType then t |> unwrapOptionType
                            else t
                        invalidOp (type' |> DesignStrings.UnknownLiteral)

        static member Literal(value: string) =
            if value.Contains(Environment.NewLine) then
                "@\"" + value.Replace("\"", "\"\"") + "\""
            else            
                "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

        static member Literal(value: bool) =
            if value then "true" else "false"

        static member Literal(value: byte) = sprintf "(byte %d)" value

        static member Literal(values: byte[]) =
            sprintf "[| %s |]" (String.Join("; ", values))

        static member Literal(value: char) =
            "\'" + (if value = '\'' then "\\'" else value.ToString()) + "\'"

        static member Literal(value: DateTime) =
            sprintf "DateTime(%d, %d, %d, %d, %d, %d,%d, DateTimeKind.%A)"
                value.Year value.Month value.Day value.Hour value.Minute value.Second value.Millisecond value.Kind

        static member Literal(value: TimeSpan) =
            sprintf "TimeSpan(%d, %d, %d, %d, %d)" value.Days value.Hours value.Minutes value.Seconds value.Milliseconds

        static member Literal(value: DateTimeOffset) =
            sprintf "DateTimeOffset(%s, %s)" (value.DateTime |> LiteralWriter.Literal) (value.Offset |> LiteralWriter.Literal)
        static member Literal(value: decimal) =
            sprintf "%fm" value
        static member Literal(value: double) = 
            (value.ToString("R", CultureInfo.InvariantCulture)) |> ensureDecimalPlaces
        static member Literal(value: float32) =
            sprintf "(float32 %f)" value
        static member Literal(value: Guid) =
            sprintf "Guid(\"%A\")" value
        static member Literal(value: int) =
            sprintf "%d" value
        static member Literal(value: Int64) =
            sprintf "%dL" value
        static member Literal(value: sbyte) =
            sprintf "(sbyte %d)" value

        static member Literal(value: Int16) =
            sprintf "%ds" value
        
        static member Literal(value: UInt32) =
            sprintf "%du" value
        static member Literal(value: UInt64) =
            sprintf "%duL" value
        static member Literal(value: UInt16) =
            sprintf "%dus" value        
        static member Literal(values: IReadOnlyList<obj>, vertical: bool) =
            
            let values' = values |> Seq.map(LiteralWriter.UnknownLiteral)
            
            if not vertical then                
                sprintf "[| %s |]" (String.Join("; ", values'))
            else
                let sb = IndentedStringBuilder()
                sb
                    |> append "[|"
                    |> indent
                    |> ignore

                values' |> Seq.iter(fun line -> sb |> appendLine line |> ignore)

                sb |> string        

        static member Literal(values: IReadOnlyList<obj>) =
            LiteralWriter.Literal (values, false)

        
        static member Literal(values: obj[,]) =
            
            let d = array2D [[0;1]; [2;3]]
            let rowCount = Array2D.length1 values
            let valuesCount = Array2D.length2 values

            let rowContents =
                [0..rowCount]
                |> Seq.map(fun i ->
                    let row' = values.[i, 0..valuesCount]
                    let entries = row' |> Seq.map(fun o -> o |> LiteralWriter.Literal)
                    sprintf "[ %s ]" (String.Join("; ", entries)) )

            sprintf "array2D [ %s ]" (String.Join("; ", rowContents))

        static member Literal(value) =
            value |> LiteralWriter.UnknownLiteral      
   
    let Lambda (properties: IReadOnlyList<string>) =
        let builder = StringBuilder()
        builder.Append("(fun x -> ") |> ignore

        match properties.Count with
        | 1 -> builder.Append("x.").Append(properties.[0]) |> ignore
        | _ -> builder.Append("(").Append(String.Join(", ", (properties |> Seq.map(fun p -> "x." + p)))).Append(" :> obj)") |> ignore

        builder.Append(")") |> string

    let Literal (o:obj) =
        o |> LiteralWriter.Literal    

    



        