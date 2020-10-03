namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.FSharp.Linq.NullableOperators
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Design

type FSharpMigrationOperationGenerator (code : ICSharpHelper) =

    let toOnedimensionalArray firstDimension (a : obj[,]) =
        Array.init a.Length (fun i -> if firstDimension then a.[i, 0] else a.[0, i])

    let sanitiseName name =
        if FSharpUtilities.isKeyword name then sprintf "``%s``" name else name

    let writeName nameValue sb =
        sb
            |> append "name = " |> appendLine (nameValue |> code.UnknownLiteral)

    let writeParameter name value sb =

        let n = sanitiseName name
        let v = value |> code.UnknownLiteral
        let fmt = sprintf ", %s = %s" n v

        sb |> append fmt

    let writeParameterIfTrue trueOrFalse name value sb =
        if trueOrFalse then
            sb |> writeParameter name value
        else
            sb

    let writeOptionalParameter (name:string) value (sb:IndentedStringBuilder) =
        sb |> writeParameterIfTrue (value |> notNull) name (sprintf "Nullable(%s)" value)

    let writeNullableParameterIfValue name (nullableParameter: Nullable<_>) sb =

        if nullableParameter.HasValue then
            let value = nullableParameter |> code.UnknownLiteral
            let fmt = sprintf ", %s = Nullable(%s)" (sanitiseName name) value

            sb |> append fmt
        else
            sb

    let annotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
        annotations
            |> Seq.iter(fun a ->
                sb
                |> appendEmptyLine
                |> append ".Annotation("
                |> append (code.Literal a.Name)
                |> append ", "
                |> append (code.UnknownLiteral a.Value)
                |> append ")"
                |> ignore
            )
        sb

    let oldAnnotations (annotations: Annotation seq) (sb:IndentedStringBuilder) =
        annotations
            |> Seq.iter(fun a ->
                sb
                |> appendEmptyLine
                |> append (sprintf ".OldAnnotation(%s, %s)" (code.Literal a.Name) (code.UnknownLiteral a.Value))
                |> ignore
            )
        sb

    let generateMigrationOperation (op:MigrationOperation) (sb:IndentedStringBuilder) :IndentedStringBuilder =
        invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation)

    let generateAddColumnOperation (op:AddColumnOperation) (sb:IndentedStringBuilder) =

        let isPropertyRequired =
            let isNullable =
                op.ClrType |> isOptionType ||
                op.ClrType |> isNullableType

            isNullable <> op.IsNullable

        sb
            |> append ".AddColumn<"
            |> append (op.ClrType |> unwrapOptionType |> code.Reference)
            |> appendLine ">("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeNullableParameterIfValue "unicode" op.IsUnicode
            |> writeNullableParameterIfValue "maxLength" op.MaxLength
            |> writeNullableParameterIfValue "fixedLength" op.IsFixedLength
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
                    id
            |> append ")"
            |> appendIfTrue (op.ClrType |> isOptionType) (sprintf ".SetValueConverter(OptionConverter<%s> ())" (op.ClrType |> unwrapOptionType |> code.Reference))

            |> annotations (op.GetAnnotations())
            |> unindent

    let generateAddForeignKeyOperation (op:AddForeignKeyOperation) (sb:IndentedStringBuilder) =

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

    let generateAddPrimaryKeyOperation (op:AddPrimaryKeyOperation) (sb:IndentedStringBuilder) =
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

    let generateAddUniqueConstraintOperation (op:AddUniqueConstraintOperation) (sb:IndentedStringBuilder) =
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

    let generateAlterColumnOperation (op:AlterColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".AlterColumn<"
            |> append (op.ClrType |> code.Reference)
            |> appendLine ">("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeOptionalParameter "type" op.ColumnType
            |> writeNullableParameterIfValue "unicode" op.IsUnicode
            |> writeNullableParameterIfValue "maxLength" op.MaxLength
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
                    id
            |> writeParameterIfTrue (op.OldColumn.ClrType |> isNull |> not) "oldClrType" (sprintf "typedefof<%s>" (op.OldColumn.ClrType |> code.Reference))
            |> writeOptionalParameter "oldType" op.OldColumn.ColumnType
            |> writeNullableParameterIfValue "oldUnicode" op.OldColumn.IsUnicode
            |> writeNullableParameterIfValue "oldMaxLength" op.OldColumn.MaxLength
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
                    id
            |> append ")"
            |> appendIfTrue (op.ClrType |> isOptionType) (sprintf ".SetValueConverter(OptionConverter<%s> ())" (op.ClrType |> unwrapOptionType |> code.Reference))
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldColumn.GetAnnotations())
            |> unindent


    let generateAlterDatabaseOperation (op:AlterDatabaseOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterDatabase()"
            |> indent
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldDatabase.GetAnnotations())
            |> unindent

    let generateAlterSequenceOperation (op:AlterSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullableParameterIfValue "minValue " op.MinValue
            |> writeNullableParameterIfValue "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> writeParameterIfTrue (op.OldSequence.IncrementBy <> 1) "oldIncrementBy" op.OldSequence.IncrementBy
            |> writeNullableParameterIfValue "oldMinValue " op.OldSequence.MinValue
            |> writeNullableParameterIfValue "oldMaxValue " op.OldSequence.MaxValue
            |> writeParameterIfTrue op.OldSequence.IsCyclic "oldCyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldSequence.GetAnnotations())
            |> unindent

    let generateAlterTableOperation (op:AlterTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".AlterTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> oldAnnotations (op.OldTable.GetAnnotations())
            |> unindent

    let generateCreateIndexOperation (op:CreateIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".CreateIndex("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
            |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
            |> writeParameterIfTrue op.IsUnique "unique" true
            |> writeOptionalParameter "filter" op.Filter
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateEnsureSchemaOperation (op:EnsureSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".EnsureSchema("
            |> indent
            |> writeName op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateCreateSequenceOperation (op:CreateSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> append ".CreateSequence"
            |>
                if op.ClrType <> typedefof<Int64> then
                    append (sprintf "<%s>" (op.ClrType |> code.Reference))
                else
                    id
            |> appendLine "("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameterIfTrue (op.StartValue <> 1L) "startValue" op.StartValue
            |> writeParameterIfTrue (op.IncrementBy <> 1) "incrementBy" op.IncrementBy
            |> writeNullableParameterIfValue "minValue " op.MinValue
            |> writeNullableParameterIfValue "maxValue " op.MaxValue
            |> writeParameterIfTrue op.IsCyclic "cyclic" "true"
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent


    let generateCreateTableOperation (op:CreateTableOperation) (sb:IndentedStringBuilder) =

        let map = Dictionary<string, string>()

        let writeColumn (c:AddColumnOperation) =
            let propertyName = c.Name |> code.Identifier
            map.Add(c.Name, propertyName)

            sb
                |> append propertyName
                |> append " = table.Column<"
                |> append (code.Reference c.ClrType)
                |> append ">("
                |> append "nullable = " |> append (c.IsNullable |> code.Literal)
                |> writeParameterIfTrue (c.Name <> propertyName) "name" c.Name
                |> writeParameterIfTrue (c.ColumnType |> notNull) "type" c.ColumnType
                |> writeNullableParameterIfValue "unicode" c.IsUnicode
                |> writeNullableParameterIfValue "maxLength" c.MaxLength
                |> writeParameterIfTrue (c.IsRowVersion) "rowVersion" c.IsRowVersion
                |>
                    if c.DefaultValueSql |> notNull then
                        append (sprintf ", defaultValueSql = %s" (c.DefaultValueSql |> code.Literal))
                    elif c.ComputedColumnSql |> notNull then
                        append (sprintf ", computedColumnSql = %s" (c.ComputedColumnSql |> code.Literal))
                    elif c.DefaultValue |> notNull then
                        append (sprintf ", defaultValue = %s" (c.DefaultValue |> code.UnknownLiteral))
                    else
                        id
                |> append ")"
                |> appendIfTrue (c.ClrType |> isOptionType) (sprintf ".SetValueConverter(OptionConverter<%s> ())" (c.ClrType |> unwrapOptionType |> code.Reference))
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

            op.Columns |> Seq.filter(notNull) |> Seq.iter(writeColumn)

            sb
                |> unindent
                |> appendLine "})"

        let writeUniqueConstraint (uc:AddUniqueConstraintOperation) =
            sb
                |> append "table.UniqueConstraint("
                |> append (uc.Name |> code.Literal)
                |> append ", "
                |> append (uc.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> code.Lambda)
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

                |> append "name = " |> append (fk.Name |> code.Literal) |> appendLine ","
                |> append (if fk.Columns.Length = 1 then "column = " else "columns = ")
                |> append (fk.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> code.Lambda)
                |> writeParameterIfTrue (fk.PrincipalSchema |> notNull) "principalSchema" fk.PrincipalSchema
                |> writeParameter "principalTable" fk.PrincipalTable
                |> writeParameterIfTrue (fk.PrincipalColumns.Length = 1) "principalColumn" fk.PrincipalColumns.[0]
                |> writeParameterIfTrue (fk.PrincipalColumns.Length <> 1) "principalColumns" fk.PrincipalColumns
                |> writeParameterIfTrue (fk.OnUpdate <> ReferentialAction.NoAction) "onUpdate" fk.OnUpdate
                |> writeParameterIfTrue (fk.OnDelete <> ReferentialAction.NoAction) "onDelete" fk.OnDelete

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
                    |> append (op.PrimaryKey.Name |> code.Literal)
                    |> append ", "
                    |> append (op.PrimaryKey.Columns |> Seq.map(fun c -> map.[c]) |> Seq.toList |> code.Lambda)
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

    let generateDropColumnOperation (op:DropColumnOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropColumn("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropForeignKeyOperation (op:DropForeignKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropForeignKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropIndexOperation (op:DropIndexOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropIndex("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropPrimaryKeyOperation (op:DropPrimaryKeyOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropPrimaryKey("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropSchemaOperation (op:DropSchemaOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSchema("
            |> indent
            |> writeName op.Name
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropSequenceOperation (op:DropSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropTableOperation (op:DropTableOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropTable("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateDropUniqueConstraintOperation (op:DropUniqueConstraintOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".DropUniqueConstraint("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "table" op.Table
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateRenameColumnOperation (op:RenameColumnOperation) (sb:IndentedStringBuilder) =
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

    let generateRenameIndexOperation (op:RenameIndexOperation) (sb:IndentedStringBuilder) =
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

    let generateRenameSequenceOperation (op:RenameSequenceOperation) (sb:IndentedStringBuilder) =
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

    let generateRenameTableOperation (op:RenameTableOperation) (sb:IndentedStringBuilder) =
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

    let generateRestartSequenceOperation (op:RestartSequenceOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine ".RestartSequence("
            |> indent
            |> writeName op.Name
            |> writeOptionalParameter "schema" op.Schema
            |> writeParameter "startValue" op.StartValue
            |> append ")"
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateSqlOperation (op:SqlOperation) (sb:IndentedStringBuilder) =
        sb
            |> appendLine (sprintf ".Sql(%s)" (op.Sql |> code.Literal))
            |> indent
            |> annotations (op.GetAnnotations())
            |> unindent

    let generateInsertDataOperation (op:InsertDataOperation) (sb:IndentedStringBuilder) =

        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s, " (op.Schema |> code.Literal)

                yield sprintf "table = %s, " (op.Table |> code.Literal)

                if op.Columns.Length = 1 then
                    yield sprintf "column = %s, " (op.Columns.[0] |> code.Literal)
                else
                    yield sprintf "columns = %s, " (op.Columns |> code.Literal)

                let length0 = op.Values.GetLength(0)
                let length1 = op.Values.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "value = %s" (op.Values.[0,0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield sprintf "values = %s" (op.Values |> toOnedimensionalArray false |> code.Literal)
                elif length1 = 1 then
                    let arr = op.Values |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "values = %s" lines
                else
                    yield sprintf "values = %s" (op.Values |> code.Literal)
            }

        sb
            |> appendLine ".InsertData("
            |> indent
            |> appendLines parameters false
            |> unindent
            |> appendLine ")"

    let generateDeleteDataOperation (op:DeleteDataOperation) (sb:IndentedStringBuilder) =
        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s, " (op.Schema |> code.Literal)

                yield sprintf "table = %s, " (op.Table |> code.Literal)

                if op.KeyColumns.Length = 1 then
                    yield sprintf "keyColumn = %s, " (op.KeyColumns.[0] |> code.Literal)
                else
                    yield sprintf "keyColumns = %s, " (op.KeyColumns |> code.Literal)

                let length0 = op.KeyValues.GetLength(0)
                let length1 = op.KeyValues.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "keyValue = %s" (op.KeyValues.[0,0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield sprintf "keyValues = %s" (op.KeyValues |> toOnedimensionalArray false |> code.Literal)
                elif length1 = 1 then
                    let arr = op.KeyValues |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "keyValues = %s" lines
                else
                    yield sprintf "keyValues = %s" (op.KeyValues |> code.Literal)
            }

        sb
            |> appendLine ".DeleteData("
            |> indent
            |> appendLines parameters false
            |> unindent
            |> appendLine ")"

    let generateUpdateDataOperation (op:UpdateDataOperation) (sb:IndentedStringBuilder) =
        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s, " (op.Schema |> code.Literal)

                yield sprintf "table = %s, " (op.Table |> code.Literal)

                if op.KeyColumns.Length = 1 then
                    yield sprintf "keyColumn = %s, " (op.KeyColumns.[0] |> code.Literal)
                else
                    yield sprintf "keyColumns = %s, " (op.KeyColumns |> code.Literal)

                let length0 = op.KeyValues.GetLength(0)
                let length1 = op.KeyValues.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "keyValue = %s" (op.KeyValues.[0,0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield sprintf "keyValues = %s" (op.KeyValues |> toOnedimensionalArray false |> code.Literal)
                elif length1 = 1 then
                    let arr = op.KeyValues |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "keyValues = %s" lines
                else
                    yield sprintf "keyValues = %s" (op.KeyValues |> code.Literal)

                if op.Columns.Length = 1 then
                    yield sprintf "column = %s, " (op.Columns.[0] |> code.Literal)
                else
                    yield sprintf "columns = %s, " (op.Columns |> code.Literal)

                let length0 = op.Values.GetLength(0)
                let length1 = op.Values.GetLength(1)

                if length0 = 1 && length1 = 1 then
                    yield sprintf "value = %s" (op.Values.[0,0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield sprintf "values = %s" (op.Values |> toOnedimensionalArray false |> code.Literal)
                elif length1 = 1 then
                    let arr = op.Values |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "values = %s" lines
                else
                    yield sprintf "values = %s" (op.Values |> code.Literal)
            }

        sb
            |> appendLine ".UpdateData("
            |> indent
            |> appendLines parameters false
            |> unindent
            |> appendLine ")"

    let generateOperation (op:MigrationOperation) =
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

    let generate (builderName:string) (operations: MigrationOperation seq) (sb:IndentedStringBuilder) =
        operations
            |> Seq.iter(fun op ->
                sb
                    |> append builderName
                    |> generateOperation op
                    |> appendLine " |> ignore"
                    |> appendEmptyLine
                    |> ignore
            )

    interface Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpMigrationOperationGenerator with
        member this.Generate(builderName, operations, builder) =
            generate builderName operations builder
