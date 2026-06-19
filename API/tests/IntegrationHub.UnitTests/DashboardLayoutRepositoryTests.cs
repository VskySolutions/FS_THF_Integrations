using FluentAssertions;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Infrastructure.Persistence.Repositories;

namespace IntegrationHub.UnitTests;

// WO-77: DashboardLayoutRepository get + upsert (insert/update) over an InMemory DbContext.
public class DashboardLayoutRepositoryTests
{
    private static DashboardLayout Layout(Guid userId, string widgetOrderJson)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WidgetOrderJson = widgetOrderJson,
            HiddenWidgetsJson = "[]",
            CollapsedWidgetsJson = "[]",
        };

    [Fact]
    public async Task GetByUserAsync_returns_null_when_no_saved_row()
    {
        await using var db = DashboardTestDbContext.Create();
        var repo = new DashboardLayoutRepository(db);

        var result = await repo.GetByUserAsync(Guid.NewGuid(), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_inserts_on_first_save()
    {
        var userId = Guid.NewGuid();
        await using var db = DashboardTestDbContext.Create();
        var repo = new DashboardLayoutRepository(db);

        await repo.UpsertAsync(Layout(userId, "[\"a\",\"b\"]"), default);
        await db.SaveChangesAsync();

        var saved = await repo.GetByUserAsync(userId, default);
        saved.Should().NotBeNull();
        saved!.WidgetOrder.Should().Equal("a", "b");
    }

    [Fact]
    public async Task UpsertAsync_updates_existing_row_json_on_second_save()
    {
        var userId = Guid.NewGuid();
        await using var db = DashboardTestDbContext.Create();
        var repo = new DashboardLayoutRepository(db);

        await repo.UpsertAsync(Layout(userId, "[\"a\"]"), default);
        await db.SaveChangesAsync();

        await repo.UpsertAsync(Layout(userId, "[\"x\",\"y\",\"z\"]"), default);
        await db.SaveChangesAsync();

        var rows = db.DashboardLayouts.Where(d => d.UserId == userId).ToList();
        rows.Should().HaveCount(1, "the existing row is updated in place, not duplicated");
        rows[0].WidgetOrder.Should().Equal("x", "y", "z");
    }
}
