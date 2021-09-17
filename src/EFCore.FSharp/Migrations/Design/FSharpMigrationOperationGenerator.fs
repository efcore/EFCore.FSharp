namespace EntityFrameworkCore.FSharp.Migrations.Design

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore.Migrations.Operations
open Microsoft.EntityFrameworkCore.Internal

open EntityFrameworkCore.FSharp.SharedTypeExtensions
open EntityFrameworkCore.FSharp.IndentedStringBuilderUtilities
open EntityFrameworkCore.FSharp.Internal
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Design
open Microsoft.EntityFrameworkCore.Migrations.Design

type FSharpMigrationOperationGenerator(code: ICSharpHelper) =

    let toOnedimensionalArray firstDimension (a: obj [,]) =
        Array.init
            a.Length
            (fun i ->
                if firstDimension then
                    a.[i, 0]
                else
                    a.[0, i])

    let sanitiseName name =
        if FSharpUtilities.isKeyword name then
            sprintf "``%s``" name
        else
            name

    let writeName nameValue sb =
        sb
        |> appendLine (sprintf "name = %s" (code.UnknownLiteral nameValue))

    let writeParameter name value sb =
        sb
        |> appendLine (sprintf ",%s = %s" (sanitiseName name) (code.UnknownLiteral value))

    let writeParameterIfTrue trueOrFalse name value sb =
        if trueOrFalse then
            sb |> writeParameter name value
        else
            sb

    let writeOptionalParameter (name: string) value (sb: IndentedStringBuilder) =
        sb
        |> writeParameterIfTrue (value |> notNull) name (sanitiseName value)

    let writeNullableParameterIfValue name (nullableParameter: Nullable<_>) sb =

        if nullableParameter.HasValue then
            let value = nullableParameter |> code.UnknownLiteral

            let fmt =
                sprintf ",%s = Nullable(%s)" (sanitiseName name) value

            sb |> appendLine fmt
        else
            sb

    let annotations (annotations: Annotation seq) (sb: IndentedStringBuilder) =

        let lines =
            annotations
            |> Seq.map (fun a -> sprintf ".Annotation(%s, %s)" (code.Literal a.Name) (code.UnknownLiteral a.Value))

        if lines |> Seq.isEmpty then
            sb
        else
            sb
            |> appendLine (lines |> Seq.head)
            |> incrementIndent
            |> appendLines (lines |> Seq.tail) true
            |> unindent

    let oldAnnotations (annotations: Annotation seq) (sb: IndentedStringBuilder) =
        let lines =
            annotations
            |> Seq.map (fun a -> sprintf ".OldAnnotation(%s, %s)" (code.Literal a.Name) (code.UnknownLiteral a.Value))

        if lines |> Seq.isEmpty then
            sb
        else
            sb
            |> appendLine (lines |> Seq.head)
            |> incrementIndent
            |> appendLines (lines |> Seq.tail) true
            |> unindent

    let generateMigrationOperation (op: MigrationOperation) (sb: IndentedStringBuilder) : IndentedStringBuilder =
        invalidOp ((op.GetType()) |> DesignStrings.UnknownOperation)

    let generateAddColumnOperation (op: AddColumnOperation) (sb: IndentedStringBuilder) =

        sb
        |> append ".AddColumn<"
        |> append (op.ClrType |> unwrapOptionType |> code.Reference)
        |> appendLine ">("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeOptionalParameter "type" op.ColumnType
        |> writeNullableParameterIfValue "unicode" op.IsUnicode
        |> writeNullableParameterIfValue "maxLength" op.MaxLength
        |> writeNullableParameterIfValue "fixedLength" op.IsFixedLength
        |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
        |> writeParameter "nullable" op.IsNullable
        |> if not (isNull op.DefaultValueSql) then
               writeParameter "defaultValueSql" op.DefaultValueSql
           elif not (isNull op.ComputedColumnSql) then
               writeParameter "computedColumnSql" op.ComputedColumnSql
           elif not (isNull op.DefaultValue) then
               writeParameter "defaultValue" op.DefaultValue
           else
               id
        |> append ")"
        |> appendIfTrue
            (op.ClrType |> isOptionType)
            (sprintf ".SetValueConverter(OptionConverter<%s> ())" (op.ClrType |> unwrapOptionType |> code.Reference))
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateAddForeignKeyOperation (op: AddForeignKeyOperation) (sb: IndentedStringBuilder) =

        sb
        |> appendLine ".AddForeignKey("
        |> incrementIndent
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
        |> unindent
        |> append ")"
        |> annotations (op.GetAnnotations())


    let generateAddPrimaryKeyOperation (op: AddPrimaryKeyOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".AddPrimaryKey("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
        |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
        |> unindent
        |> append ")"
        |> annotations (op.GetAnnotations())

    let generateAddUniqueConstraintOperation (op: AddUniqueConstraintOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".AddUniqueConstraint("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeParameterIfTrue (op.Columns.Length = 1) "column" op.Columns.[0]
        |> writeParameterIfTrue (op.Columns.Length <> 1) "columns" op.Columns
        |> unindent
        |> append ")"
        |> annotations (op.GetAnnotations())

    let generateAlterColumnOperation (op: AlterColumnOperation) (sb: IndentedStringBuilder) =
        sb
        |> append ".AlterColumn<"
        |> append (op.ClrType |> code.Reference)
        |> appendLine ">("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeOptionalParameter "type" op.ColumnType
        |> writeNullableParameterIfValue "unicode" op.IsUnicode
        |> writeNullableParameterIfValue "maxLength" op.MaxLength
        |> writeParameterIfTrue op.IsRowVersion "rowVersion" true
        |> writeParameter "nullable" op.IsNullable
        |> if op.DefaultValueSql |> notNull then
               writeParameter "defaultValueSql" op.DefaultValueSql
           elif op.ComputedColumnSql |> notNull then
               writeParameter "computedColumnSql" op.ComputedColumnSql
           elif op.DefaultValue |> notNull then
               writeParameter "defaultValue" op.DefaultValue
           else
               id
        |> if op.OldColumn.ClrType |> isNull |> not then
               (fun sb ->
                   sb
                   |> append (sprintf ",oldClrType = typedefof<%s>" (op.OldColumn.ClrType |> code.Reference))
                   |> appendEmptyLine)
           else
               id
        |> writeOptionalParameter "oldType" op.OldColumn.ColumnType
        |> writeNullableParameterIfValue "oldUnicode" op.OldColumn.IsUnicode
        |> writeNullableParameterIfValue "oldMaxLength" op.OldColumn.MaxLength
        |> writeParameterIfTrue op.OldColumn.IsRowVersion "oldRowVersion" true
        |> writeParameter "oldNullable" op.OldColumn.IsNullable
        |> if op.OldColumn.DefaultValueSql |> notNull then
               writeParameter "oldDefaultValueSql" op.OldColumn.DefaultValueSql
           elif op.OldColumn.ComputedColumnSql |> notNull then
               writeParameter "oldComputedColumnSql" op.OldColumn.ComputedColumnSql
           elif op.OldColumn.DefaultValue |> notNull then
               writeParameter "oldDefaultValue" op.OldColumn.DefaultValue
           else
               id
        |> append ")"
        |> appendIfTrue
            (op.ClrType |> isOptionType)
            (sprintf ".SetValueConverter(OptionConverter<%s> ())" (op.ClrType |> unwrapOptionType |> code.Reference))
        |> annotations (op.GetAnnotations())
        |> oldAnnotations (op.OldColumn.GetAnnotations())
        |> unindent


    let generateAlterDatabaseOperation (op: AlterDatabaseOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".AlterDatabase()"
        |> incrementIndent
        |> annotations (op.GetAnnotations())
        |> oldAnnotations (op.OldDatabase.GetAnnotations())
        |> unindent

    let generateAlterSequenceOperation (op: AlterSequenceOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".AlterSequence("
        |> incrementIndent
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

    let generateAlterTableOperation (op: AlterTableOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".AlterTable("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> oldAnnotations (op.OldTable.GetAnnotations())
        |> unindent

    let generateCreateIndexOperation (op: CreateIndexOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".CreateIndex("
        |> incrementIndent
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

    let generateEnsureSchemaOperation (op: EnsureSchemaOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".EnsureSchema("
        |> incrementIndent
        |> writeName op.Name
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateCreateSequenceOperation (op: CreateSequenceOperation) (sb: IndentedStringBuilder) =
        sb
        |> append ".CreateSequence"
        |> if op.ClrType <> typedefof<Int64> then
               append (sprintf "<%s>" (op.ClrType |> code.Reference))
           else
               id
        |> appendLine "("
        |> incrementIndent
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


    let generateCreateTableOperation (op: CreateTableOperation) (sb: IndentedStringBuilder) =

        let map = Dictionary<string, string>()

        let writeColumn (c: AddColumnOperation) =
            let propertyName = c.Name |> code.Identifier
            map.Add(c.Name, propertyName)

            sb
            |> appendLine (sprintf "%s =" propertyName)
            |> incrementIndent
            |> append "table.Column<"
            |> append (code.Reference c.ClrType)
            |> appendLine ">("
            |> incrementIndent
            |> appendLine (sprintf "nullable = %s" (code.Literal c.IsNullable))
            |> writeParameterIfTrue (c.Name <> propertyName) "name" c.Name
            |> writeParameterIfTrue (c.ColumnType |> notNull) "type" c.ColumnType
            |> writeNullableParameterIfValue "unicode" c.IsUnicode
            |> writeNullableParameterIfValue "maxLength" c.MaxLength
            |> writeParameterIfTrue (c.IsRowVersion) "rowVersion" c.IsRowVersion
            |> if c.DefaultValueSql |> notNull then
                   appendLine (sprintf ", defaultValueSql = %s" (code.Literal c.DefaultValueSql))
               elif c.ComputedColumnSql |> notNull then
                   appendLine (sprintf ", computedColumnSql = %s" (code.Literal c.ComputedColumnSql))
               elif c.DefaultValue |> notNull then
                   appendLine (sprintf ", defaultValue = %s" (code.UnknownLiteral c.DefaultValue))
               else
                   id
            |> unindent
            |> append ")"
            |> appendIfTrue
                (c.ClrType |> isOptionType)
                (sprintf ".SetValueConverter(OptionConverter<%s> ())" (c.ClrType |> unwrapOptionType |> code.Reference))
            |> incrementIndent
            |> annotations (c.GetAnnotations())
            |> unindent
            |> unindent
            |> appendEmptyLine
            |> ignore

        let writeColumns sb =

            sb
            |> appendLine ",columns = (fun table -> "
            |> appendLine "{|"
            |> incrementIndent
            |> ignore

            op.Columns
            |> Seq.filter notNull
            |> Seq.iter writeColumn

            sb |> unindent |> appendLine "|})"

        let writeUniqueConstraint (uc: AddUniqueConstraintOperation) =
            sb
            |> append "table.UniqueConstraint("
            |> append (uc.Name |> code.Literal)
            |> append ", "
            |> append (
                uc.Columns
                |> Seq.map (fun c -> map.[c])
                |> Seq.toList
                |> code.Lambda
            )
            |> append ")"
            |> incrementIndent
            |> annotations (op.PrimaryKey.GetAnnotations())
            |> appendLine " |> ignore"
            |> unindent
            |> ignore

        let writeForeignKeyConstraint (fk: AddForeignKeyOperation) =
            sb
            |> appendLine "table.ForeignKey("
            |> incrementIndent

            |> append "name = "
            |> append (fk.Name |> code.Literal)
            |> appendLine ","
            |> append (
                if fk.Columns.Length = 1 then
                    "column = "
                else
                    "columns = "
            )
            |> appendLine (
                fk.Columns
                |> Seq.map (fun c -> map.[c])
                |> Seq.toList
                |> code.Lambda
            )
            |> writeParameterIfTrue (fk.PrincipalSchema |> notNull) "principalSchema" fk.PrincipalSchema
            |> writeParameter "principalTable" fk.PrincipalTable
            |> writeParameterIfTrue (fk.PrincipalColumns.Length = 1) "principalColumn" fk.PrincipalColumns.[0]
            |> writeParameterIfTrue (fk.PrincipalColumns.Length <> 1) "principalColumns" fk.PrincipalColumns
            |> writeParameterIfTrue (fk.OnUpdate <> ReferentialAction.NoAction) "onUpdate" fk.OnUpdate
            |> writeParameterIfTrue (fk.OnDelete <> ReferentialAction.NoAction) "onDelete" fk.OnDelete

            |> append ")"
            |> annotations (fk.GetAnnotations())
            |> appendLine " |> ignore"
            |> appendEmptyLine
            |> unindent
            |> ignore

            ()

        let writeConstraints sb =

            let hasConstraints =
                notNull op.PrimaryKey
                || (op.UniqueConstraints |> Seq.isEmpty |> not)
                || (op.ForeignKeys |> Seq.isEmpty |> not)

            if hasConstraints then

                sb
                |> append ","
                |> appendLine "constraints ="
                |> incrementIndent
                |> appendLine "(fun table -> "
                |> incrementIndent
                |> ignore

                if notNull op.PrimaryKey then

                    let pkName = op.PrimaryKey.Name |> code.Literal

                    let pkColumns =
                        op.PrimaryKey.Columns
                        |> Seq.map (fun c -> map.[c])
                        |> Seq.toList
                        |> code.Lambda

                    sb
                    |> append (sprintf "table.PrimaryKey(%s, %s)" pkName pkColumns)
                    |> annotations (op.PrimaryKey.GetAnnotations())
                    |> appendLine " |> ignore"
                    |> ignore

                op.UniqueConstraints
                |> Seq.iter writeUniqueConstraint

                op.ForeignKeys
                |> Seq.iter writeForeignKeyConstraint

                sb |> unindent |> appendLine ") " |> unindent
            else
                sb

        sb
        |> appendLine ".CreateTable("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeColumns
        |> writeConstraints
        |> unindent
        |> append ")"
        |> annotations (op.GetAnnotations())

    let generateDropColumnOperation (op: DropColumnOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropColumn("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropForeignKeyOperation (op: DropForeignKeyOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropForeignKey("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropIndexOperation (op: DropIndexOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropIndex("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropPrimaryKeyOperation (op: DropPrimaryKeyOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropPrimaryKey("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropSchemaOperation (op: DropSchemaOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropSchema("
        |> incrementIndent
        |> writeName op.Name
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropSequenceOperation (op: DropSequenceOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropSequence("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropTableOperation (op: DropTableOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropTable("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateDropUniqueConstraintOperation (op: DropUniqueConstraintOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".DropUniqueConstraint("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateRenameColumnOperation (op: RenameColumnOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".RenameColumn("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeParameter "newName" op.NewName
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateRenameIndexOperation (op: RenameIndexOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".RenameIndex("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "table" op.Table
        |> writeParameter "newName" op.NewName
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateRenameSequenceOperation (op: RenameSequenceOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".RenameSequence("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "newName" op.NewName
        |> writeParameter "newSchema" op.NewSchema
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateRenameTableOperation (op: RenameTableOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".RenameTable("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "newName" op.NewName
        |> writeParameter "newSchema" op.NewSchema
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateRestartSequenceOperation (op: RestartSequenceOperation) (sb: IndentedStringBuilder) =
        sb
        |> appendLine ".RestartSequence("
        |> incrementIndent
        |> writeName op.Name
        |> writeOptionalParameter "schema" op.Schema
        |> writeParameter "startValue" op.StartValue
        |> append ")"
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateSqlOperation (op: SqlOperation) (sb: IndentedStringBuilder) =
        sb
        |> append (sprintf ".Sql(%s)" (op.Sql |> code.Literal))
        |> incrementIndent
        |> annotations (op.GetAnnotations())
        |> unindent

    let generateInsertDataOperation (op: InsertDataOperation) (sb: IndentedStringBuilder) =

        let parameters =
            seq {
                if notNull op.Schema then
                    yield sprintf "schema = %s," (op.Schema |> code.Literal)

                yield sprintf "table = %s," (op.Table |> code.Literal)

                if op.Columns.Length = 1 then
                    yield sprintf "column = %s," (op.Columns.[0] |> code.Literal)
                else
                    yield sprintf "columns = %s," (op.Columns |> code.Literal)

                let length0 = op.Values.GetLength(0)
                let length1 = op.Values.GetLength(1)

                let valuesArray =
                    if length0 = 1 && length1 = 1 then
                        sprintf "value = %s :> obj" (op.Values.[0, 0] |> code.UnknownLiteral)
                    elif length0 = 1 then
                        sprintf
                            "values = %s"
                            (op.Values
                             |> toOnedimensionalArray false
                             |> code.Literal)
                    elif length1 = 1 then
                        let arr = op.Values |> toOnedimensionalArray true
                        let lines = code.Literal(arr, true)
                        sprintf "values = %s" lines
                    else
                        sprintf "values = %s" (op.Values |> code.Literal)

                yield valuesArray
            }

        sb
        |> appendLine ".InsertData("
        |> incrementIndent
        |> appendLines parameters false
        |> unindent
        |> append ")"

    let generateDeleteDataOperation (op: DeleteDataOperation) (sb: IndentedStringBuilder) =
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
                    yield sprintf "keyValue = %s" (op.KeyValues.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "keyValues = %s"
                            (op.KeyValues
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr =
                        op.KeyValues |> toOnedimensionalArray true

                    let lines = code.Literal(arr, true)
                    yield sprintf "keyValues = %s" lines
                else
                    yield sprintf "keyValues = %s" (op.KeyValues |> code.Literal)
            }

        sb
        |> appendLine ".DeleteData("
        |> incrementIndent
        |> appendLines parameters false
        |> unindent
        |> appendLine ")"

    let generateUpdateDataOperation (op: UpdateDataOperation) (sb: IndentedStringBuilder) =
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
                    yield sprintf "keyValue = %s" (op.KeyValues.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "keyValues = %s"
                            (op.KeyValues
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr =
                        op.KeyValues |> toOnedimensionalArray true

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
                    yield sprintf "value = %s" (op.Values.[0, 0] |> code.UnknownLiteral)
                elif length0 = 1 then
                    yield
                        sprintf
                            "values = %s"
                            (op.Values
                             |> toOnedimensionalArray false
                             |> code.Literal)
                elif length1 = 1 then
                    let arr = op.Values |> toOnedimensionalArray true
                    let lines = code.Literal(arr, true)
                    yield sprintf "values = %s" lines
                else
                    yield sprintf "values = %s" (op.Values |> code.Literal)
            }

        sb
        |> appendLine ".UpdateData("
        |> incrementIndent
        |> appendLines parameters false
        |> unindent
        |> appendLine ")"

    let generateOperation (op: MigrationOperation) =
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

    let generate (builderName: string) (operations: MigrationOperation seq) (sb: IndentedStringBuilder) =

        if operations |> Seq.isEmpty then
            sb |> appendLine "()" |> ignore
        else
            operations
            |> Seq.iter
                (fun op ->
                    sb
                    |> append builderName
                    |> generateOperation op
                    |> appendLine " |> ignore"
                    |> appendEmptyLine
                    |> ignore)

    interface ICSharpMigrationOperationGenerator with
        member this.Generate(builderName, operations, builder) = generate builderName operations builder
