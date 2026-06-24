namespace IntegrationHub.Domain.Enums;

/// <summary>
/// The set of platform entity types that Universal Features (notes, tags, attachments,
/// activity, reminders, pins, colour codes, checklists, modified-log, …) can attach to via the
/// shared <c>(EntityType, EntityId)</c> key pattern (Universal Features ADR-001).
/// <para>
/// Values are stable integers seeded in application code, not the database. New entity types are
/// added by extending this enum — no schema change or migration is required for the UF tables to
/// serve them.
/// </para>
/// </summary>
public enum EntityType
{
    CustomerRequest = 1,
    IntegrationJob = 2,
    Tenant = 3,
    User = 4,
    UserGroup = 5,
}
