namespace Bricelam.EntityFrameworkCore.FSharp

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations.Design
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities

module FSharpMigrationOperationGenerator =

    type OperationWriter =

        static member Generate (operation:MigrationOperation, sb:IndentedStringBuilder) :unit =
            // TODO: implement        
            invalidOp ((operation.GetType()) |> DesignStrings.UnknownOperation)

        static member Generate (operation:AddColumnOperation, sb:IndentedStringBuilder) =
            // TODO: implement        
            ()

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
