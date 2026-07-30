using EmsPortal.Application.Abstractions.Security;
using EmsPortal.Application.Abstractions.Tenancy;
using EmsPortal.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmsPortal.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for the EmsPortal application schema. Owns the
/// application tables (customers, access management, email, option sets, universal
/// features, audit trail). Schema migrations are applied by the Integration API on
/// startup; the Background Worker and MCP Server are read/write consumers only.
/// <para>
/// Tenant isolation is enforced here: tenant-scoped entities carry a global query
/// filter on the resolved <see cref="ITenantContext.TenantId"/>, and inserts are
/// stamped with the active tenant. When no tenant is resolved (background/global
/// operations, or design-time), the filter is a no-op.
/// </para>
/// </summary>
public class EmsPortalDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IActorAccessor _actorAccessor;

    public EmsPortalDbContext(
        DbContextOptions<EmsPortalDbContext> options,
        ITenantContext tenantContext,
        IActorAccessor actorAccessor)
        : base(options)
    {
        _tenantContext = tenantContext;
        _actorAccessor = actorAccessor;
    }

    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserTenantRole> UserTenantRoles => Set<UserTenantRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<Media> Media => Set<Media>();

    public DbSet<CustomerRequest> CustomerRequests => Set<CustomerRequest>();

    public DbSet<CustomerAuditEntry> CustomerAuditEntries => Set<CustomerAuditEntry>();

    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();

    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();

    public DbSet<PermissionGroupPermission> PermissionGroupPermissions => Set<PermissionGroupPermission>();

    public DbSet<RolePermissionGroup> RolePermissionGroups => Set<RolePermissionGroup>();

    public DbSet<PermissionGroupTemplate> PermissionGroupTemplates => Set<PermissionGroupTemplate>();

    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();

    public DbSet<SmtpAccount> SmtpAccounts => Set<SmtpAccount>();

    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    // ---- Option Sets (tenant-configurable input value lists) ----
    public DbSet<OptionSet> OptionSets => Set<OptionSet>();
    public DbSet<OptionSetItem> OptionSetItems => Set<OptionSetItem>();

    // ---- Universal Features (Phase 14) ----
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteMention> NoteMentions => Set<NoteMention>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntityTag> EntityTags => Set<EntityTag>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Pin> Pins => Set<Pin>();
    public DbSet<ColourCode> ColourCodes => Set<ColourCode>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<StickyNote> StickyNotes => Set<StickyNote>();
    public DbSet<StickyNoteDismissal> StickyNoteDismissals => Set<StickyNoteDismissal>();
    public DbSet<UserStickyNoteState> UserStickyNoteStates => Set<UserStickyNoteState>();
    public DbSet<DeletedRecordRetentionConfig> DeletedRecordRetentionConfigs => Set<DeletedRecordRetentionConfig>();
    public DbSet<FieldModifiedLog> FieldModifiedLogs => Set<FieldModifiedLog>();
    public DbSet<ModifiedLogFieldConfig> ModifiedLogFieldConfigs => Set<ModifiedLogFieldConfig>();

    /// <summary>Data Protection key ring storage (Multi-Tenancy ADR-002).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmsPortalDbContext).Assembly);

        // Tenant isolation filters, combined with the soft-delete filter. The tenant
        // filter is bypassed when no tenant is resolved so global background operations
        // still work; soft-deleted rows are always excluded.
        modelBuilder.Entity<AuditTrailEntry>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<CustomerRequest>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<CustomerAuditEntry>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<CustomerDocument>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<PermissionGroup>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        // Persons are CRM master records owned by a tenant; scope them so a non-Super-Admin never
        // sees another tenant's people. Self-profile reads bypass this filter via GetByUserIdAsync.
        modelBuilder.Entity<Person>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        // User groups + memberships are tenant-scoped so a tenant only ever sees its own groups.
        modelBuilder.Entity<UserGroup>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<UserGroupMember>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        // SMTP accounts are tenant-scoped; a tenant only ever sees its own mail accounts.
        modelBuilder.Entity<SmtpAccount>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);

        // Universal Features (Phase 14): every UF table is tenant-scoped + soft-deletable, so it
        // carries the combined ambient-tenant + soft-delete filter. FieldModifiedLog is the lone
        // exception — it is append-only with no soft-delete column, so it uses a tenant-only filter.
        modelBuilder.Entity<Note>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<NoteMention>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Tag>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<EntityTag>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Attachment>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<ActivityEvent>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Reminder>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<NotificationPreference>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Pin>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<ColourCode>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<SavedView>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<Checklist>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<ChecklistItem>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<StickyNote>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<StickyNoteDismissal>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<UserStickyNoteState>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<DeletedRecordRetentionConfig>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<ModifiedLogFieldConfig>().HasQueryFilter(e => (!_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId) && !e.Deleted);
        modelBuilder.Entity<FieldModifiedLog>().HasQueryFilter(e => !_tenantContext.IsResolved || e.TenantId == _tenantContext.TenantId);

        // Soft-delete filters for the non-tenant-scoped entities.
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<UserTenantRole>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => !e.Deleted);
        // Email templates carry a nullable TenantId (null = platform default); a tenant filter would
        // hide the defaults, so they use the soft-delete filter only and are scoped explicitly in the repo.
        modelBuilder.Entity<EmailTemplate>().HasQueryFilter(e => !e.Deleted);
        // Option sets/items carry a nullable TenantId (null = platform-standard list); a tenant filter
        // would hide the standard lists, so they use the soft-delete filter only and are scoped
        // explicitly in the repository (standard rows ∪ the current tenant's rows).
        modelBuilder.Entity<OptionSet>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<OptionSetItem>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<Role>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<TenantRole>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<Address>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<Media>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<PermissionGroupTemplate>().HasQueryFilter(e => !e.Deleted);
        modelBuilder.Entity<DashboardLayout>().HasQueryFilter(e => !e.Deleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTenant();
        StampAudit();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps audit fields and converts deletes into soft-deletes. Created* is set on
    /// insert, Updated* on every change, and a requested delete becomes a Modified row
    /// flagged <see cref="AuditableEntity.Deleted"/> rather than being physically removed.
    /// </summary>
    private void StampAudit()
    {
        var now = DateTime.UtcNow;
        var actorId = Guid.TryParse(_actorAccessor.GetCurrentActor(), out var id) ? id : (Guid?)null;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = now;
                    entry.Entity.CreatedById = actorId;
                    entry.Entity.UpdatedOnUtc = now;
                    entry.Entity.UpdatedById = actorId;
                    entry.Entity.Deleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedOnUtc = now;
                    entry.Entity.UpdatedById = actorId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.Deleted = true;
                    entry.Entity.DeletedOnUtc = now;
                    entry.Entity.UpdatedOnUtc = now;
                    entry.Entity.UpdatedById = actorId;
                    break;
            }
        }
    }

    /// <summary>Stamps the resolved tenant id on newly added tenant-scoped entities.</summary>
    private void StampTenant()
    {
        if (!_tenantContext.IsResolved)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            switch (entry.Entity)
            {
                case AuditTrailEntry audit when audit.TenantId == Guid.Empty:
                    audit.TenantId = _tenantContext.TenantId;
                    break;
                case CustomerRequest customer when customer.TenantId == Guid.Empty:
                    customer.TenantId = _tenantContext.TenantId;
                    break;
                case CustomerAuditEntry auditEntry when auditEntry.TenantId == Guid.Empty:
                    auditEntry.TenantId = _tenantContext.TenantId;
                    break;
                case CustomerDocument document when document.TenantId == Guid.Empty:
                    document.TenantId = _tenantContext.TenantId;
                    break;
                case PermissionGroup permissionGroup when permissionGroup.TenantId == Guid.Empty:
                    permissionGroup.TenantId = _tenantContext.TenantId;
                    break;
                case Person person when person.TenantId is null || person.TenantId == Guid.Empty:
                    person.TenantId = _tenantContext.TenantId;
                    break;
                case UserGroup userGroup when userGroup.TenantId == Guid.Empty:
                    userGroup.TenantId = _tenantContext.TenantId;
                    break;
                case UserGroupMember member when member.TenantId == Guid.Empty:
                    member.TenantId = _tenantContext.TenantId;
                    break;
                case SmtpAccount smtpAccount when smtpAccount.TenantId == Guid.Empty:
                    smtpAccount.TenantId = _tenantContext.TenantId;
                    break;

                // ---- Universal Features (Phase 14) ----
                case Note note when note.TenantId == Guid.Empty:
                    note.TenantId = _tenantContext.TenantId;
                    break;
                case NoteMention noteMention when noteMention.TenantId == Guid.Empty:
                    noteMention.TenantId = _tenantContext.TenantId;
                    break;
                case Tag tag when tag.TenantId == Guid.Empty:
                    tag.TenantId = _tenantContext.TenantId;
                    break;
                case EntityTag entityTag when entityTag.TenantId == Guid.Empty:
                    entityTag.TenantId = _tenantContext.TenantId;
                    break;
                case Attachment attachment when attachment.TenantId == Guid.Empty:
                    attachment.TenantId = _tenantContext.TenantId;
                    break;
                case ActivityEvent activityEvent when activityEvent.TenantId == Guid.Empty:
                    activityEvent.TenantId = _tenantContext.TenantId;
                    break;
                case Reminder reminder when reminder.TenantId == Guid.Empty:
                    reminder.TenantId = _tenantContext.TenantId;
                    break;
                case Notification notification when notification.TenantId == Guid.Empty:
                    notification.TenantId = _tenantContext.TenantId;
                    break;
                case NotificationPreference notificationPreference when notificationPreference.TenantId == Guid.Empty:
                    notificationPreference.TenantId = _tenantContext.TenantId;
                    break;
                case Pin pin when pin.TenantId == Guid.Empty:
                    pin.TenantId = _tenantContext.TenantId;
                    break;
                case ColourCode colourCode when colourCode.TenantId == Guid.Empty:
                    colourCode.TenantId = _tenantContext.TenantId;
                    break;
                case SavedView savedView when savedView.TenantId == Guid.Empty:
                    savedView.TenantId = _tenantContext.TenantId;
                    break;
                case Checklist checklist when checklist.TenantId == Guid.Empty:
                    checklist.TenantId = _tenantContext.TenantId;
                    break;
                case ChecklistItem checklistItem when checklistItem.TenantId == Guid.Empty:
                    checklistItem.TenantId = _tenantContext.TenantId;
                    break;
                case StickyNote stickyNote when stickyNote.TenantId == Guid.Empty:
                    stickyNote.TenantId = _tenantContext.TenantId;
                    break;
                case StickyNoteDismissal stickyNoteDismissal when stickyNoteDismissal.TenantId == Guid.Empty:
                    stickyNoteDismissal.TenantId = _tenantContext.TenantId;
                    break;
                case UserStickyNoteState userStickyNoteState when userStickyNoteState.TenantId == Guid.Empty:
                    userStickyNoteState.TenantId = _tenantContext.TenantId;
                    break;
                case DeletedRecordRetentionConfig retentionConfig when retentionConfig.TenantId == Guid.Empty:
                    retentionConfig.TenantId = _tenantContext.TenantId;
                    break;
                case FieldModifiedLog fieldModifiedLog when fieldModifiedLog.TenantId == Guid.Empty:
                    fieldModifiedLog.TenantId = _tenantContext.TenantId;
                    break;
                case ModifiedLogFieldConfig modifiedLogFieldConfig when modifiedLogFieldConfig.TenantId == Guid.Empty:
                    modifiedLogFieldConfig.TenantId = _tenantContext.TenantId;
                    break;
            }
        }
    }
}
