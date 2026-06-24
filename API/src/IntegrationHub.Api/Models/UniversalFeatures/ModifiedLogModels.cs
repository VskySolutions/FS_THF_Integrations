using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Api.Models.UniversalFeatures;

/// <summary>A single field change-history entry.</summary>
public sealed record ModifiedLogEntryResponse(
    Guid Id,
    string FieldName,
    string? OldValue,
    string? NewValue,
    Guid? ChangedById,
    string ChangedByName,
    DateTime ChangedOnUtc);

/// <summary>A tracked field's tenant configuration row, for the admin config matrix.</summary>
public sealed record ModifiedLogConfigResponse(
    string FieldKey,
    EntityType EntityType,
    string FieldName,
    string DisplayName,
    bool IsEnabled,
    bool IsSystemTracked);

/// <summary>Request to toggle an optional tracked field on/off for the tenant.</summary>
public sealed class ToggleModifiedLogConfigRequest
{
    public bool IsEnabled { get; set; }
}
