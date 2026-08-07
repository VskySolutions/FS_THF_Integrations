using EmsPortal.Application.Abstractions.Persistence;

namespace EmsPortal.Infrastructure.Persistence;

/// <summary>
/// Generates per-tenant <c>REMS-{seq}</c> request numbers where <c>seq</c> is the active request count
/// for the tenant plus one, probing forward past any already-taken number. Allocate inside
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>; the filtered unique index
/// <c>(TenantId, REMSNumber) WHERE [Deleted] = 0</c> is the definitive guard under concurrency.
/// </summary>
internal sealed class RemsNumberGenerator : IRemsNumberGenerator
{
    private readonly IRemsRepository _rems;

    public RemsNumberGenerator(IRemsRepository rems)
    {
        _rems = rems;
    }

    public async Task<string> GenerateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var seq = await _rems.CountActiveByTenantAsync(tenantId, cancellationToken) + 1;
        var number = $"REMS-{seq}";
        while (await _rems.NumberExistsAsync(tenantId, number, cancellationToken))
        {
            seq++;
            number = $"REMS-{seq}";
        }

        return number;
    }
}
