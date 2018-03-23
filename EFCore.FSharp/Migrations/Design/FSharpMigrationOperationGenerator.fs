namespace Bricelam.EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.FSharp.Linq.NullableOperators
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open Bricelam.EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open Bricelam.EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations

module FSharpMigrationOperationGenerator =

    let private writeName nameValue sb =
        sb
            |> append "name = " |> appendLine (nameValue |> FSharpHelper.Literal)
    
    let private writeParameter name value sb =
        sb
            |> append ","
            |> append name
            |> append " = "
            |> appendLine (value |> FSharpHelper.Literal)

    let private writeParameterIfTrue trueOrFalse name value sb =
        match trueOrFalse with
        | true -> sb |> writeParameter name value
        | false -> sb

    let private writeOptionalParameter (name:string) value (sb:IndentedStringBuilder) =
        sb |> writeParameterIfTrue (value |> notNull) name value

    let private writeNullable name (nullableParamter: Nullable<_>) sb =
        sb |> writeParameterIfTrue nullableParamter.HasValue name nullableParamter.Value

    let private annotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
        annotations
            |> Seq.iter(fun a ->
                sb
                |> appendEmptyLine
                |> append ".Annotation("
                |> append (FSharpHelper.Literal a.Name)
                |> append ", "
                |> append (FSharpHelper.UnknownLiteral a.Value)
                |> append ")"
                |> ignore
            )
        sb

    let private oldAnnotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
        annotations
            |> Seq.iter(fun a ->
                sb
                |> appendEmptyLine
                |> append ".OldAnnotation("
                |> append (FSharpHelper.Literal a.Name)
                |> append ", "
                |> append (FSharpHelper.UnknownLiteral a.Value)
                |> append ")"
                |> ignore
            )
        sb

    let private generateMigrationOperation (op:MigrationOperation) (sb:IndentedStringBuilder) :IndentedStringBuilder =
        invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation)

    let private generateAddColumnOperation (op:AddColumnOperation) (sb:IndentedStringBuilder) =

        sb
            |> append ".AddColumn<"
            |> append (op.ClrType |> FSharpHelper.Reference)
            |> appendLine ">("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
            |> writeNullable "maxLength" op.MaxLength
            |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
            |> writeParameter "nullable" op.IsNullable
            |>
                if not(isNull op.DefaultValueSql) then
                    writeParameter "defaultValueSql" op.DefaultValueSql
                elif not(isNull op.ComputedColumnSql) then
                    writeParameter "computedColumnSql" op.ComputedColumnSql
                elif not(isNull op.DefaultValue) then
                    writeParameter "defaultValue" op.DefaultValue
                else
                    noop
            |> append ")"                

            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddForeignKeyOperation (op:AddForeignKeyOperation) (sb:IndentedStringBuilder) =

        sb
            |> appendLine ".AddForeignKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> writeOptionalParameter "principalSchema" op.PrincipalSchema
            |> writeParameter "principalTable" op.PrincipalTable
            |> writeParameterIfTrue (op.PrincipalColumns.Length = 1) "principalColumn" op.PrincipalColumns.[0]
            |> writeParameterIfTrue (op.PrincipalColumns.Length <> 1) "principalColumns" op.PrincipalColumns
            |> writeParameterIfTrue (op.OnUpdate <> ReferentialAction.NoAction) "onUpdate" op.OnUpdate
            |> writeParameterIfTrue (op.OnDelete <> ReferentialAction.NoAction) "onDelete" op.OnDelete
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddPrimaryKeyOperation (op:AddPrimaryKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AddPrimaryKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAddUniqueConstraintOperation (op:AddUniqueConstraintOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AddUniqueConstraint("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateAlterColumnOperation (op:AlterColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".AlterColumn<"
            |> append (op.ClrType |> FSharpHelper.Reference)
            |> appendLine ">("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeParameterIfTrue (op.IsUnicode ?= false) "unicode" "false"
            |> writeNullable "maxLength" op.MaxLength
            |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
            |> writeParameter "nullable" op.IsNullable
            |>
                if op.DefaultValueSql |> notNull then
                    writeParameter "defaultValueSql" op.DefaultValueSql
                elif op.ComputedColumnSql |> notNull then
                    writeParameter "computedColumnSql" op.ComputedColumnSql
                elif op.DefaultValue |> notNull then
                    writeParameter "defaultValue" op.DefaultValue
                else
                    noop
            |> writeParameterIfTrue (op.OldColumn.ClrType |> isNull |> not) "oldType" (sprintf "typedefof<%s>" (op.OldColumn.ClrType |> FSharpHelper.Reference))
            |> writeOptionalParameter "oldType" op.OldColumn.ColumnType
            |> writeParameterIfTrue (op.OldColumn.IsUnicode ?= false) "oldUnicode" "false"
            |> writeNullable "oldMaxLength" op.OldColumn.MaxLength
            |> writeParameterIfTrue op.OldColumn.IsRowVersion "oldRowVersion" true
            |> writeParameter "oldNullable" op.OldColumn.IsNullable
            |>
                if op.OldColumn.DefaultValueSql |> notNull then
                    writeParameter "oldDefaultValueSql" op.OldColumn.DefaultValueSql
                elif op.OldColumn.ComputedColumnSql |> notNull then
                    writeParameter "oldComputedColumnSql" op.OldColumn.ComputedColumnSql
                elif op.OldColumn.DefaultValue |> notNull then
                    writeParameter "oldDefaultValue" op.OldColumn.DefaultValue
                else
                    noop
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldColumn.GetAnnotations())
            |> unindent


    let private generateAlterDatabaseOperation (op:AlterDatabaseOperation) (sb:IndentedStringBuilder) =            
        sb
            |> appendLine ".AlterDatabase()"
            |> indent
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldDatabase.GetAnnotations())
            |> unindent

    let private generateAlterSequenceOperation (op:AlterSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullable "minValue " op.MinValue
            |> writeNullable "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> writeParameterIfTrue (op.OldSequence.IncrementBy <> 1) "oldIncrementBy" op.OldSequence.IncrementBy
            |> writeNullable "oldMinValue " op.OldSequence.MinValue
            |> writeNullable "oldMaxValue " op.OldSequence.MaxValue
            |> writeParameterIfTrue op.OldSequence.IsCyclic "oldCyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldSequence.GetAnnotations())
            |> unindent

    let private generateAlterTableOperation (op:AlterTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldTable.GetAnnotations())
            |> unindent

    let private generateCreateIndexOperation (op:CreateIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".CreateIndex("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> writeParameterIfTrue op.IsUnique "unique" "true"
            |> writeOptionalParameter "filter" op.Filter
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateEnsureSchemaOperation (op:EnsureSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".EnsureSchema("
            |> indent
            |> writeName op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateCreateSequenceOperation (op:CreateSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".CreateSequence"
            |>
                if op.ClrType <> typedefof<Int64> then
                    append (sprintf "<%s>" (op.ClrType |> FSharpHelper.Reference))
                else
                    noop
            |> appendLine "("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.StartValue <> 1L) "startValue" op.StartValue
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullable "minValue " op.MinValue
            |> writeNullable "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent


    let private generateCreateTableOperation (op:CreateTableOperation) (sb:IndentedStringBuilder) =
        
        let map = Dictionary<string, string>()

        let appendIfTrue truth name value b =
                if truth then
                    b |> append (sprintf ", %s = %s" name (value |> FSharpHelper.Literal))
                else
                    b

        let appendIfHasValue name (value: Nullable<_>) b =
                if value.HasValue then
                    b |> append (sprintf ", %s = %s" name (value.Value |> FSharpHelper.Literal))
                else
                    b                

        let writeColumn (c:AddColumnOperation) =
            let propertyName = c.Name |> FSharpHelper.Identifier
            map.Add(c.Name, propertyName)            

            sb
                |> append propertyName
                |> append " = table.Column<"
                |> append (FSharpHelper.Reference(c.ClrType))
                |> append ">("
                |> append "nullable = " |> append (c.IsNullable |> FSharpHelper.Literal)
                |> appendIfTrue (c.Name <> propertyName) "name" c.Name
                |> appendIfTrue (c.ColumnType |> notNull) "type" c.ColumnType
                |> appendIfTrue (c.IsUnicode.HasValue && (not c.IsUnicode.Value)) "unicode" false
                |> appendIfHasValue "maxLength" c.MaxLength
                |> appendIfTrue (c.IsRowVersion) "rowVersion" c.IsRowVersion
                |>
                    if c.DefaultValueSql |> notNull then
                        append (sprintf ", defaultValueSql = %s" (c.DefaultValueSql |> FSharpHelper.Literal))
                    elif c.ComputedColumnSql |> notNull then
                        append (sprintf ", computedColumnSql = %s" (c.ComputedColumnSql |> FSharpHelper.Literal))
                    elif c.DefaultValue |> notNull then
                        append (sprintf ", defaultValue = %s" (c.DefaultValue |> FSharpHelper.UnknownLiteral))
                    else
                        noop
                |> append ")"
                |> indent
                |> annotations (c.GetAnnotations())
                |> unindent
                |> appendEmptyLine
                |> ignore

        let writeColumns sb =

            sb
                |> append "," |> appendLine "columns = (fun table -> "
                |> appendLine "{"
                |> indent
                |> ignore

            op.Columns |> Seq.filter(fun c  -> c |> notNull) |> Seq.iter(writeColumn)

            sb
                |> unindent
                |> appendLine "})"

        let writeUniqueConstraint (uc:AddUniqueConstraintOperation) =
            sb
                |> append "table.UniqueConstraint("
                |> append (uc.Name |> FSharpHelper.Literal)
                |> append ", "
                |> append (uc.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> FSharpHelper.Lambda)
                |> append ")"
                |> indent
                |> annotations (op.PrimaryKey.GetAnnotations())
                |> appendLine " |> ignore"
                |> unindent
                |> ignore

        let writeForeignKeyConstraint (fk:AddForeignKeyOperation) =
            sb
                |> appendLine "table.ForeignKey("
                |> indent

                |> append "name = " |> append (fk.Name |> FSharpHelper.Literal) |> appendLine ","
                |> append (if fk.Columns.Length = 1 then "column = " else "columns = ")
                |> append (fk.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> FSharpHelper.Lambda)
                |> appendIfTrue (fk.PrincipalSchema |> notNull) "principalSchema" fk.PrincipalSchema
                |> appendIfTrue true "principalTable" fk.PrincipalTable
                |> appendIfTrue (fk.PrincipalColumns.Length = 1) "principalColumn" fk.PrincipalColumns.[0]
                |> appendIfTrue (fk.PrincipalColumns.Length <> 1) "principalColumns" fk.PrincipalColumns
                |> appendIfTrue (fk.OnUpdate <> ReferentialAction.NoAction) "onUpdate" fk.OnUpdate
                |> appendIfTrue (fk.OnDelete <> ReferentialAction.NoAction) "onDelete" fk.OnDelete

                |> append ")"                
                |> annotations (fk.GetAnnotations())
                |> unindent
                |> appendLine " |> ignore"                
                |> appendEmptyLine
                |> ignore
            ()

        let writeConstraints sb =
            sb |> append "," |> appendLine "constraints ="
            |> indent
            |> appendLine "(fun table -> "
            |> indent
            |> ignore

            if op.PrimaryKey |> notNull then
                sb
                    |> append "table.PrimaryKey("
                    |> append (op.PrimaryKey.Name |> FSharpHelper.Literal)
                    |> append ", "
                    |> append (op.PrimaryKey.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> FSharpHelper.Lambda)
                    |> appendLine ") |> ignore"
                    |> indent
                    |> annotations (op.PrimaryKey.GetAnnotations())
                    |> unindent
                    |> ignore
            
            op.UniqueConstraints |> Seq.iter(writeUniqueConstraint)
            op.ForeignKeys |> Seq.iter(writeForeignKeyConstraint)

            sb
                |> unindent
                |> appendLine ") "       
        
        sb
            |> appendLine ".CreateTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeColumns
            |> writeConstraints
            |> unindent
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropColumnOperation (op:DropColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropColumn("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropForeignKeyOperation (op:DropForeignKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropForeignKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropIndexOperation (op:DropIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropIndex("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropPrimaryKeyOperation (op:DropPrimaryKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropPrimaryKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropSchemaOperation (op:DropSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSchema("
            |> indent
            |> writeName op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropSequenceOperation (op:DropSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropTableOperation (op:DropTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateDropUniqueConstraintOperation (op:DropUniqueConstraintOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropUniqueConstraint("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameColumnOperation (op:RenameColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameColumn("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameter "newName" op.NewName
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameIndexOperation (op:RenameIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameIndex("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameter "newName" op.NewName
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameSequenceOperation (op:RenameSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "newName" op.NewName
            |> writeParameter "newSchema" op.NewSchema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRenameTableOperation (op:RenameTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RenameTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "newName" op.NewName
            |> writeParameter "newSchema" op.NewSchema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateRestartSequenceOperation (op:RestartSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RestartSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "startValue" op.StartValue
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateSqlOperation (op:SqlOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine (sprintf ".Sql(%s)" (op.Sql |> FSharpHelper.Literal))
            |> indent
            |> annotations (op.GetAnnotations())
            |> unindent

    let private generateInsertDataOperation (op:InsertDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateDeleteDataOperation (op:DeleteDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateUpdateDataOperation (op:UpdateDataOperation) (sb:IndentedStringBuilder) =
        // TODO: implement
        sb

    let private generateOperation (op:MigrationOperation) =
        match op with
        | :? AddColumnOperation as op' -> op' |> generateAddColumnOperation
        | :? AddForeignKeyOperation as op' -> op' |> generateAddForeignKeyOperation
        | :? AddPrimaryKeyOperation as op' -> op' |> generateAddPrimaryKeyOperation
        | :? AddUniqueConstraintOperation as op' -> op' |> generateAddUniqueConstraintOperation
        | :? AlterColumnOperation as op' -> op' |> generateAlterColumnOperation
        | :? AlterDatabaseOperation as op' -> op' |> generateAlterDatabaseOperation
        | :? AlterSequenceOperation as op' -> op' |> generateAlterSequenceOperation
        | :? AlterTableOperation as op' -> op' |> generateAlterTableOperation
        | :? CreateIndexOperation as op' -> op' |> generateCreateIndexOperation
        | :? EnsureSchemaOperation as op' -> op' |> generateEnsureSchemaOperation
        | :? CreateSequenceOperation as op' -> op' |> generateCreateSequenceOperation
        | :? CreateTableOperation as op' -> op' |> generateCreateTableOperation
        | :? DropColumnOperation as op' -> op' |> generateDropColumnOperation
        | :? DropForeignKeyOperation as op' -> op' |> generateDropForeignKeyOperation
        | :? DropIndexOperation as op' -> op' |> generateDropIndexOperation
        | :? DropPrimaryKeyOperation as op' -> op' |> generateDropPrimaryKeyOperation
        | :? DropSchemaOperation as op' -> op' |> generateDropSchemaOperation
        | :? DropSequenceOperation as op' -> op' |> generateDropSequenceOperation
        | :? DropTableOperation as op' -> op' |> generateDropTableOperation
        | :? DropUniqueConstraintOperation as op' -> op' |> generateDropUniqueConstraintOperation
        | :? RenameColumnOperation as op' -> op' |> generateRenameColumnOperation
        | :? RenameIndexOperation as op' -> op' |> generateRenameIndexOperation
        | :? RenameSequenceOperation as op' -> op' |> generateRenameSequenceOperation
        | :? RenameTableOperation as op' -> op' |> generateRenameTableOperation
        | :? RestartSequenceOperation as op' -> op' |> generateRestartSequenceOperation
        | :? SqlOperation as op' -> op' |> generateSqlOperation
        | :? InsertDataOperation as op' -> op' |> generateInsertDataOperation
        | :? DeleteDataOperation as op' -> op' |> generateDeleteDataOperation
        | :? UpdateDataOperation as op' -> op' |> generateUpdateDataOperation
        | _ -> op |> generateMigrationOperation // The failure case

    let Generate (builderName:string) (operations: MigrationOperation list) (sb:IndentedStringBuilder) =
        operations
            |> Seq.iter(fun op -> 
                sb
                    |> append builderName
                    |> generateOperation op
                    |> appendLine " |> ignore"
                    |> appendEmptyLine
                    |> ignore
            )
        sb
