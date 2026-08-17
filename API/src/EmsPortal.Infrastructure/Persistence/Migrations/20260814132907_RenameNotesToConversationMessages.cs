using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Renames the Notes feature to Conversations: a record's thread is now a set of
    /// <c>ConversationMessages</c> keyed on <c>(EntityType, EntityId)</c>.
    /// <para>
    /// Hand-written as renames. EF scaffolded this as drop-and-create — it sees two entities removed and
    /// two added, not a rename — which would have destroyed every existing note and mention. Every
    /// statement here preserves the rows.
    /// </para>
    /// </summary>
    public partial class RenameNotesToConversationMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Constraints first, while their current names still match the objects they hang off.
            migrationBuilder.Sql("EXEC sp_rename N'FK_NoteMentions_Notes_NoteId', N'FK_ConversationMessageMentions_ConversationMessages_ConversationMessageId', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_NoteMentions', N'PK_ConversationMessageMentions', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_Notes', N'PK_ConversationMessages', N'OBJECT';");

            // Indexes are addressed as 'Table.Index', so rename them before the tables move.
            migrationBuilder.RenameIndex(
                name: "IX_Notes_EntityType_EntityId",
                newName: "IX_ConversationMessages_EntityType_EntityId",
                table: "Notes");

            migrationBuilder.RenameIndex(
                name: "IX_Notes_TenantId",
                newName: "IX_ConversationMessages_TenantId",
                table: "Notes");

            migrationBuilder.RenameIndex(
                name: "IX_NoteMentions_NoteId",
                newName: "IX_ConversationMessageMentions_ConversationMessageId",
                table: "NoteMentions");

            migrationBuilder.RenameIndex(
                name: "IX_NoteMentions_MentionedUserId_IsRead",
                newName: "IX_ConversationMessageMentions_MentionedUserId_IsRead",
                table: "NoteMentions");

            migrationBuilder.RenameIndex(
                name: "IX_NoteMentions_TenantId",
                newName: "IX_ConversationMessageMentions_TenantId",
                table: "NoteMentions");

            migrationBuilder.RenameColumn(
                name: "NoteId",
                table: "NoteMentions",
                newName: "ConversationMessageId");

            migrationBuilder.RenameTable(name: "NoteMentions", newName: "ConversationMessageMentions");
            migrationBuilder.RenameTable(name: "Notes", newName: "ConversationMessages");

            // History is part of the rename: an activity row still reading "NoteAdded" would render as an
            // unknown event type in the timeline, which now maps the new key.
            migrationBuilder.Sql("UPDATE [ActivityEvents] SET [EventType] = 'ConversationMessageAdded' WHERE [EventType] = 'NoteAdded';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [ActivityEvents] SET [EventType] = 'NoteAdded' WHERE [EventType] = 'ConversationMessageAdded';");

            migrationBuilder.RenameTable(name: "ConversationMessages", newName: "Notes");
            migrationBuilder.RenameTable(name: "ConversationMessageMentions", newName: "NoteMentions");

            migrationBuilder.RenameColumn(
                name: "ConversationMessageId",
                table: "NoteMentions",
                newName: "NoteId");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessageMentions_TenantId",
                newName: "IX_NoteMentions_TenantId",
                table: "NoteMentions");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessageMentions_MentionedUserId_IsRead",
                newName: "IX_NoteMentions_MentionedUserId_IsRead",
                table: "NoteMentions");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessageMentions_ConversationMessageId",
                newName: "IX_NoteMentions_NoteId",
                table: "NoteMentions");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessages_TenantId",
                newName: "IX_Notes_TenantId",
                table: "Notes");

            migrationBuilder.RenameIndex(
                name: "IX_ConversationMessages_EntityType_EntityId",
                newName: "IX_Notes_EntityType_EntityId",
                table: "Notes");

            migrationBuilder.Sql("EXEC sp_rename N'PK_ConversationMessages', N'PK_Notes', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'PK_ConversationMessageMentions', N'PK_NoteMentions', N'OBJECT';");
            migrationBuilder.Sql("EXEC sp_rename N'FK_ConversationMessageMentions_ConversationMessages_ConversationMessageId', N'FK_NoteMentions_Notes_NoteId', N'OBJECT';");
        }
    }
}
