namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open System.Security.Cryptography.X509Certificates
open Microsoft.EntityFrameworkCore.Infrastructure

module FSharpMigrationOperationGenerator =

    type OperationWriter =        

        static member private writeParameter name value sb =
            sb
                |> appendLine ","
                |> append name |> append " = " |> appendLine (value |> FSharpHelper.Literal)

        static member private writeParameterIfTrue trueOrFalse name value sb =
            match trueOrFalse with
            | true -> sb |> OperationWriter.writeParameter name value
            | false -> sb

        static member private writeOptionalParameter (name:string) value (sb:IndentedStringBuilder) =
            sb |> OperationWriter.writeParameterIfTrue (isNull value |> not) name value

        static member private writeNullableBool name (nullableParameter: Nullable<bool>) sb =
            let truth = (nullableParameter.HasValue && not nullableParameter.Value)
            sb |> OperationWriter.writeParameterIfTrue truth name nullableParameter.Value

        static member private writeNullableInt name (nullableParamter: Nullable<int>) sb =
            sb |> OperationWriter.writeParameterIfTrue nullableParamter.HasValue name nullableParamter.Value

        static member Annotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
            annotations
                |> Seq.iter(fun a ->
                    sb
                    |> appendLine ""
                    |> append ".Annotation("
                    |> append (FSharpHelper.LiteralWriter.Literal a.Name)
                    |> append ", "
                    |> append (FSharpHelper.LiteralWriter.UnknownLiteral a.Value)
                    |> append ")"
                    |> ignore
                )

        static member Generate (operation:MigrationOperation, sb:IndentedStringBuilder) :unit =
            // TODO: implement        
            invalidOp ((operation.GetType()) |> DesignStrings.UnknownOperation)

        static member Generate (operation:AddColumnOperation, sb:IndentedStringBuilder) =
            
            sb
                |> append ".AddColumn<"
                |> append (operation.ClrType |> FSharpHelper.Reference)
                |> appendLine ">("
                |> indent
                |> OperationWriter.writeParameter "name" operation.Name
                |> OperationWriter.writeOptionalParameter "schema" operation.Schema
                |> OperationWriter.writeParameter "table" operation.Table
                |> OperationWriter.writeOptionalParameter "type" operation.ColumnType
                |> OperationWriter.writeNullableBool "unicode" operation.IsUnicode
                |> OperationWriter.writeNullableInt "maxLength" operation.MaxLength
                |> OperationWriter.writeParameterIfTrue operation.IsRowVersion "rowVersion" true
                |> OperationWriter.writeParameter "nullable" operation.IsNullable
                |>
                    if not(isNull operation.DefaultValueSql) then
                        OperationWriter.writeParameter "defaultValueSql" operation.DefaultValueSql
                    elif not(isNull operation.ComputedColumnSql) then
                        OperationWriter.writeParameter "computedColumnSql" operation.ComputedColumnSql
                    elif not(isNull operation.DefaultValue) then
                        OperationWriter.writeParameter "defaultValue" operation.DefaultValue
                    else
                        append ""
                |> append ")"                                                                              

                |> OperationWriter.Annotations (operation.GetAnnotations())



        static member Generate (operation:AddForeignKeyOperation, sb:IndentedStringBuilder) =
            // TODO: implement        
            ()

        static member Generate (operation:AddPrimaryKeyOperation, sb:IndentedStringBuilder) =
            // TODO: implement        
            ()

        static member Generate (operation:AddUniqueConstraintOperation, sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:AlterColumnOperation, sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:AlterDatabaseOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:AlterSequenceOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:AlterTableOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:CreateIndexOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:EnsureSchemaOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:CreateSequenceOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:CreateTableOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropColumnOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropForeignKeyOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropIndexOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropPrimaryKeyOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropSchemaOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropSequenceOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropTableOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DropUniqueConstraintOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:RenameColumnOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:RenameIndexOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:RenameSequenceOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:RenameTableOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:RestartSequenceOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:SqlOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:InsertDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:DeleteDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

        static member Generate (operation:UpdateDataOperation , sb:IndentedStringBuilder) =
            // TODO: implement
            ()

    let Generate (builderName:string) (operations: IReadOnlyList<MigrationOperation>) (sb:IndentedStringBuilder) =
        // TODO: implement

        operations
            |> Seq.iter(fun op -> 

                sb |> appendLine builderName |> ignore

                match op with
                | :? AddColumnOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AddForeignKeyOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AddPrimaryKeyOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AddUniqueConstraintOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AlterColumnOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AlterDatabaseOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AlterSequenceOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? AlterTableOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? CreateIndexOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? EnsureSchemaOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? CreateSequenceOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? CreateTableOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropColumnOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropForeignKeyOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropIndexOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropPrimaryKeyOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropSchemaOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropSequenceOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropTableOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DropUniqueConstraintOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? RenameColumnOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? RenameIndexOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? RenameSequenceOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? RenameTableOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? RestartSequenceOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? SqlOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? InsertDataOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? DeleteDataOperation as op' -> (op', sb) |> OperationWriter.Generate
                | :? UpdateDataOperation as op' -> (op', sb) |> OperationWriter.Generate
                | _ -> (op, sb) |> OperationWriter.Generate // The failure case

                sb
                    |> appendLine ""
                    |> appendLine ""
                    |> ignore
            )

        sb
