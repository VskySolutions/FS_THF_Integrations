namespace EmsPortal.Domain.Enums;

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
    Tenant = 3,
    User = 4,
    UserGroup = 5,
    Rems = 6,
    // Added so Deleted Records Management can serve every administered list, not just user groups.
    // Values are append-only: they are persisted on UF rows, so renumbering would silently re-point
    // every note, tag and attachment already written against them.
    Person = 7,
    Role = 8,
    OptionSet = 9,
    PermissionGroup = 10,
    SmtpAccount = 11,
    EmailTemplate = 12,
    Tag = 13,
    SavedView = 14,
    StickyNote = 15,
}
