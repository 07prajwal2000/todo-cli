using Todo.Domain.Tasks;

namespace Todo.Domain.Tests;

public sealed class TaskSchemaTests
{
    [Fact]
    public void ColumnsListsAllEightInTheDocumentedOrder()
    {
        Assert.Equal(
            ["id", "name", "description", "status", "due_on", "created_at", "completed_at", "remarks"],
            TaskSchema.Columns);
    }

    [Fact]
    public void RequiredColumnsIsASubsetOfColumns()
    {
        Assert.NotEmpty(TaskSchema.RequiredColumns);

        foreach (var required in TaskSchema.RequiredColumns)
        {
            Assert.Contains(required, TaskSchema.Columns);
        }
    }

    [Fact]
    public void RequiredColumnsAreTheFourLoadBearingOnes()
    {
        Assert.Equal(["id", "name", "status", "created_at"], TaskSchema.RequiredColumns);
    }

    [Fact]
    public void ColumnNamesAreUnique()
    {
        Assert.Equal(TaskSchema.Columns.Count, TaskSchema.Columns.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SchemaVersionIsOne()
    {
        Assert.Equal(1, TaskSchema.Version);
    }

    [Fact]
    public void NextIdKeyNamespacesTheSpaceName()
    {
        Assert.Equal("next_id:work", TaskSchema.NextIdKey("work"));
        Assert.Equal("next_id:my space", TaskSchema.NextIdKey("my space"));
    }

    [Fact]
    public void NextIdKeyPrefixMatchesTheGeneratedKey()
    {
        Assert.StartsWith(TaskSchema.NextIdKeyPrefix, TaskSchema.NextIdKey("work"), StringComparison.Ordinal);
    }

    [Fact]
    public void MetaTabConstantsDescribeTheMetaSheet()
    {
        Assert.Equal("_meta", TaskSchema.MetaTabName);
        Assert.Equal("key", TaskSchema.MetaKeyColumn);
        Assert.Equal("value", TaskSchema.MetaValueColumn);
        Assert.Equal("schema_version", TaskSchema.SchemaVersionKey);
    }
}
