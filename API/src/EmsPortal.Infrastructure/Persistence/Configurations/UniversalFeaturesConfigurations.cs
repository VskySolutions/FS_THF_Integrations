using EmsPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmsPortal.Infrastructure.Persistence.Configurations;

// EF Core configurations for the Universal Features tables. All carry a TenantId and are subject to
// the ambient tenant + soft-delete global query filter declared in EmsPortalDbContext.

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.EntityType).HasConversion<int>().IsRequired();
        builder.Property(n => n.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(n => new { n.EntityType, n.EntityId });
        builder.HasIndex(n => n.TenantId);
        builder.HasMany(n => n.Mentions)
            .WithOne(m => m.Note!)
            .HasForeignKey(m => m.NoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NoteMentionConfiguration : IEntityTypeConfiguration<NoteMention>
{
    public void Configure(EntityTypeBuilder<NoteMention> builder)
    {
        builder.ToTable("NoteMentions");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.NoteId);
        builder.HasIndex(m => new { m.MentionedUserId, m.IsRead });
        builder.HasIndex(m => m.TenantId);
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Colour).HasMaxLength(30);
        builder.Property(t => t.Category).HasMaxLength(100);
        builder.HasIndex(t => new { t.TenantId, t.Name }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasMany(t => t.EntityTags)
            .WithOne(e => e.Tag!)
            .HasForeignKey(e => e.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EntityTagConfiguration : IEntityTypeConfiguration<EntityTag>
{
    public void Configure(EntityTypeBuilder<EntityTag> builder)
    {
        builder.ToTable("EntityTags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasConversion<int>().IsRequired();
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.TagId }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(e => e.TenantId);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).HasConversion<int>().IsRequired();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(260);
        builder.Property(a => a.StoredPath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.MimeType).HasMaxLength(150);
        builder.Property(a => a.FileExtension).HasMaxLength(20);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.TenantId);
    }
}

internal sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).HasConversion<int>().IsRequired();
        builder.Property(a => a.EventType).IsRequired().HasMaxLength(60);
        builder.Property(a => a.OldValue).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValue).HasColumnType("nvarchar(max)");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedOnUtc }).IsDescending(false, false, true);
        builder.HasIndex(a => a.TenantId);
    }
}

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.EntityType).HasConversion<int>().IsRequired();
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.IsDispatched, r.DueAtUtc });
        builder.HasIndex(r => r.TenantId);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<int>().IsRequired();
        builder.Property(n => n.EntityType).HasConversion<int>();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).HasColumnType("nvarchar(max)");
        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => new { n.UserId, n.CreatedOnUtc }).IsDescending(false, true);
        builder.HasIndex(n => n.TenantId);
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.NotificationType).HasConversion<int>().IsRequired();
        builder.HasIndex(p => new { p.UserId, p.NotificationType }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(p => p.TenantId);
    }
}

internal sealed class PinConfiguration : IEntityTypeConfiguration<Pin>
{
    public void Configure(EntityTypeBuilder<Pin> builder)
    {
        builder.ToTable("Pins");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.EntityType).HasConversion<int>().IsRequired();
        builder.HasIndex(p => new { p.UserId, p.EntityType, p.EntityId }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(p => p.TenantId);
    }
}

internal sealed class ColourCodeConfiguration : IEntityTypeConfiguration<ColourCode>
{
    public void Configure(EntityTypeBuilder<ColourCode> builder)
    {
        builder.ToTable("ColourCodes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.EntityType).HasConversion<int>().IsRequired();
        builder.Property(c => c.Colour).IsRequired().HasMaxLength(30);
        builder.HasIndex(c => new { c.UserId, c.EntityType, c.EntityId }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(c => c.TenantId);
    }
}

internal sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("SavedViews");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(150);
        builder.Property(v => v.ListPage).IsRequired().HasMaxLength(100);
        builder.Property(v => v.FiltersJson).HasColumnType("nvarchar(max)");
        builder.Property(v => v.SortJson).HasColumnType("nvarchar(max)");
        builder.Property(v => v.ColumnsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(v => new { v.TenantId, v.ListPage });
        builder.HasIndex(v => v.UserId);
    }
}

internal sealed class ChecklistConfiguration : IEntityTypeConfiguration<Checklist>
{
    public void Configure(EntityTypeBuilder<Checklist> builder)
    {
        builder.ToTable("Checklists");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.EntityType).HasConversion<int>().IsRequired();
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => new { c.EntityType, c.EntityId });
        builder.HasIndex(c => c.TenantId);
        builder.HasMany(c => c.Items)
            .WithOne(i => i.Checklist!)
            .HasForeignKey(i => i.ChecklistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Text).IsRequired().HasMaxLength(500);
        builder.HasIndex(i => i.ChecklistId);
        builder.HasIndex(i => i.TenantId);
    }
}

internal sealed class StickyNoteConfiguration : IEntityTypeConfiguration<StickyNote>
{
    public void Configure(EntityTypeBuilder<StickyNote> builder)
    {
        builder.ToTable("StickyNotes");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Title).HasMaxLength(200);
        builder.Property(s => s.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(s => s.Colour).IsRequired().HasMaxLength(30);
        builder.Property(s => s.Scope).IsRequired().HasMaxLength(300);
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.CreatedByUserId);
        builder.HasMany(s => s.Dismissals)
            .WithOne(d => d.StickyNote!)
            .HasForeignKey(d => d.StickyNoteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.States)
            .WithOne(st => st.StickyNote!)
            .HasForeignKey(st => st.StickyNoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StickyNoteDismissalConfiguration : IEntityTypeConfiguration<StickyNoteDismissal>
{
    public void Configure(EntityTypeBuilder<StickyNoteDismissal> builder)
    {
        builder.ToTable("StickyNoteDismissals");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => new { d.StickyNoteId, d.UserId }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(d => d.TenantId);
    }
}

internal sealed class UserStickyNoteStateConfiguration : IEntityTypeConfiguration<UserStickyNoteState>
{
    public void Configure(EntityTypeBuilder<UserStickyNoteState> builder)
    {
        builder.ToTable("UserStickyNoteStates");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.StickyNoteId, s.UserId }).IsUnique().HasFilter("[Deleted] = 0");
        builder.HasIndex(s => s.TenantId);
    }
}

internal sealed class DeletedRecordRetentionConfigConfiguration : IEntityTypeConfiguration<DeletedRecordRetentionConfig>
{
    public void Configure(EntityTypeBuilder<DeletedRecordRetentionConfig> builder)
    {
        builder.ToTable("DeletedRecordRetentionConfigs");
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.TenantId).IsUnique().HasFilter("[Deleted] = 0");
    }
}

internal sealed class FieldModifiedLogConfiguration : IEntityTypeConfiguration<FieldModifiedLog>
{
    public void Configure(EntityTypeBuilder<FieldModifiedLog> builder)
    {
        builder.ToTable("FieldModifiedLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.EntityType).HasConversion<int>().IsRequired();
        builder.Property(l => l.FieldName).IsRequired().HasMaxLength(150);
        builder.Property(l => l.OldValue).HasColumnType("nvarchar(max)");
        builder.Property(l => l.NewValue).HasColumnType("nvarchar(max)");
        // Fast history queries newest-first per (entity, field).
        builder.HasIndex(l => new { l.EntityType, l.EntityId, l.FieldName, l.ChangedOnUtc })
            .IsDescending(false, false, false, true);
        builder.HasIndex(l => l.TenantId);
    }
}

internal sealed class ModifiedLogFieldConfigConfiguration : IEntityTypeConfiguration<ModifiedLogFieldConfig>
{
    public void Configure(EntityTypeBuilder<ModifiedLogFieldConfig> builder)
    {
        builder.ToTable("ModifiedLogFieldConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.EntityType).HasConversion<int>().IsRequired();
        builder.Property(c => c.FieldName).IsRequired().HasMaxLength(150);
        builder.HasIndex(c => new { c.TenantId, c.EntityType, c.FieldName }).IsUnique().HasFilter("[Deleted] = 0");
    }
}
