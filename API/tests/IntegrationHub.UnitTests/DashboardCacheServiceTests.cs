using FluentAssertions;
using IntegrationHub.Api.Dashboard;
using Microsoft.Extensions.Caching.Memory;

namespace IntegrationHub.UnitTests;

// WO-77: DashboardCacheService memoization + forceRefresh eviction.
public class DashboardCacheServiceTests
{
    private static DashboardCacheService Create() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetOrAddAsync_runs_factory_on_miss_and_returns_value()
    {
        var calls = 0;
        var service = Create();

        var value = await service.GetOrAddAsync("k", forceRefresh: false, () =>
        {
            calls++;
            return Task.FromResult(42);
        });

        value.Should().Be(42);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrAddAsync_second_hit_returns_cached_value_without_rerunning_factory()
    {
        var calls = 0;
        var service = Create();
        Func<Task<int>> factory = () =>
        {
            calls++;
            return Task.FromResult(calls); // would change if re-run
        };

        var first = await service.GetOrAddAsync("k", forceRefresh: false, factory);
        var second = await service.GetOrAddAsync("k", forceRefresh: false, factory);

        first.Should().Be(1);
        second.Should().Be(1, "the cached value is returned without re-running the factory");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrAddAsync_force_refresh_evicts_and_reruns_factory()
    {
        var calls = 0;
        var service = Create();
        Func<Task<int>> factory = () =>
        {
            calls++;
            return Task.FromResult(calls);
        };

        var first = await service.GetOrAddAsync("k", forceRefresh: false, factory);
        var refreshed = await service.GetOrAddAsync("k", forceRefresh: true, factory);

        first.Should().Be(1);
        refreshed.Should().Be(2, "forceRefresh evicts the entry and re-runs the factory");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetOrAddAsync_keys_are_isolated()
    {
        var service = Create();

        var a = await service.GetOrAddAsync("a", false, () => Task.FromResult("A"));
        var b = await service.GetOrAddAsync("b", false, () => Task.FromResult("B"));

        a.Should().Be("A");
        b.Should().Be("B");
    }

    // NOTE: TTL expiry (60s) is intentionally NOT timing-tested. DashboardCacheService hard-codes a
    // 60s IMemoryCache TTL with no injectable clock/TTL seam, so a deterministic expiry test is not
    // possible without a real 60s wait; skipped per the WO-77 test plan.
}
