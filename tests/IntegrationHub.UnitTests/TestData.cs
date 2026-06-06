using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.UnitTests;

/// <summary>Builders for common domain entities used across the unit tests.</summary>
internal static class TestData
{
    public static IntegrationJob Job(Guid? id = null, Guid? tenantId = null, IntegrationJobStatus status = IntegrationJobStatus.Created)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            InterfaceName = "ExpenseImport",
            Direction = IntegrationDirection.Inbound,
            SourceSystem = SystemName.Concur,
            TargetSystem = SystemName.Maconomy,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
        };

    public static User User(string email = "user@test.local", bool isActive = true, int tokenVersion = 1, bool mustChangePassword = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "hash",
            Salt = "salt",
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            TokenVersion = tokenVersion,
            CreatedDate = DateTime.UtcNow,
        };

    public static Tenant Tenant(string identifier = "acme", TenantStatus status = TenantStatus.Active)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            Identifier = identifier,
            Status = status,
            CreatedDate = DateTime.UtcNow,
        };

    public static UserTenantRole Assignment(Guid userId, Guid tenantId, UserRole role)
        => new() { Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId, Role = role };
}
