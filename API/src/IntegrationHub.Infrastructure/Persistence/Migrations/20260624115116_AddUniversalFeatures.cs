using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColourCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColourCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedRecordRetentionConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedRecordRetentionConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldModifiedLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldModifiedLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModifiedLogFieldConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifiedLogFieldConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    EditedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    InApp = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    IsGrouped = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDispatched = table.Column<bool>(type: "bit", nullable: false),
                    IsOverdue = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ListPage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StickyNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsPersonal = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickyNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteMentions_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StickyNoteDismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StickyNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickyNoteDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StickyNoteDismissals_StickyNotes_StickyNoteId",
                        column: x => x.StickyNoteId,
                        principalTable: "StickyNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserStickyNoteStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StickyNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    IsMinimised = table.Column<bool>(type: "bit", nullable: false),
                    ZIndex = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStickyNoteStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserStickyNoteStates_StickyNotes_StickyNoteId",
                        column: x => x.StickyNoteId,
                        principalTable: "StickyNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntityTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntityTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_EntityType_EntityId_CreatedOnUtc",
                table: "ActivityEvents",
                columns: new[] { "EntityType", "EntityId", "CreatedOnUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_TenantId",
                table: "ActivityEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_EntityType_EntityId",
                table: "Attachments",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TenantId",
                table: "Attachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ChecklistId",
                table: "ChecklistItems",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_TenantId",
                table: "ChecklistItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_EntityType_EntityId",
                table: "Checklists",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_TenantId",
                table: "Checklists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ColourCodes_TenantId",
                table: "ColourCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ColourCodes_UserId_EntityType_EntityId",
                table: "ColourCodes",
                columns: new[] { "UserId", "EntityType", "EntityId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedRecordRetentionConfigs_TenantId",
                table: "DeletedRecordRetentionConfigs",
                column: "TenantId",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_EntityType_EntityId",
                table: "EntityTags",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_EntityType_EntityId_TagId",
                table: "EntityTags",
                columns: new[] { "EntityType", "EntityId", "TagId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_TagId",
                table: "EntityTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityTags_TenantId",
                table: "EntityTags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldModifiedLogs_EntityType_EntityId_FieldName_ChangedOnUtc",
                table: "FieldModifiedLogs",
                columns: new[] { "EntityType", "EntityId", "FieldName", "ChangedOnUtc" },
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FieldModifiedLogs_TenantId",
                table: "FieldModifiedLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifiedLogFieldConfigs_TenantId_EntityType_FieldName",
                table: "ModifiedLogFieldConfigs",
                columns: new[] { "TenantId", "EntityType", "FieldName" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NoteMentions_MentionedUserId_IsRead",
                table: "NoteMentions",
                columns: new[] { "MentionedUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteMentions_NoteId",
                table: "NoteMentions",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteMentions_TenantId",
                table: "NoteMentions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_EntityType_EntityId",
                table: "Notes",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_TenantId",
                table: "Notes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_TenantId",
                table: "NotificationPreferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_NotificationType",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "NotificationType" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedOnUtc",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedOnUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Pins_TenantId",
                table: "Pins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Pins_UserId_EntityType_EntityId",
                table: "Pins",
                columns: new[] { "UserId", "EntityType", "EntityId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_IsDispatched_DueAtUtc",
                table: "Reminders",
                columns: new[] { "IsDispatched", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TenantId",
                table: "Reminders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_UserId",
                table: "Reminders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_TenantId_ListPage",
                table: "SavedViews",
                columns: new[] { "TenantId", "ListPage" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId",
                table: "SavedViews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StickyNoteDismissals_StickyNoteId_UserId",
                table: "StickyNoteDismissals",
                columns: new[] { "StickyNoteId", "UserId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StickyNoteDismissals_TenantId",
                table: "StickyNoteDismissals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StickyNotes_CreatedByUserId",
                table: "StickyNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StickyNotes_TenantId",
                table: "StickyNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TenantId_Name",
                table: "Tags",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserStickyNoteStates_StickyNoteId_UserId",
                table: "UserStickyNoteStates",
                columns: new[] { "StickyNoteId", "UserId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserStickyNoteStates_TenantId",
                table: "UserStickyNoteStates",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "ColourCodes");

            migrationBuilder.DropTable(
                name: "DeletedRecordRetentionConfigs");

            migrationBuilder.DropTable(
                name: "EntityTags");

            migrationBuilder.DropTable(
                name: "FieldModifiedLogs");

            migrationBuilder.DropTable(
                name: "ModifiedLogFieldConfigs");

            migrationBuilder.DropTable(
                name: "NoteMentions");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Pins");

            migrationBuilder.DropTable(
                name: "Reminders");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "StickyNoteDismissals");

            migrationBuilder.DropTable(
                name: "UserStickyNoteStates");

            migrationBuilder.DropTable(
                name: "Checklists");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "StickyNotes");
        }
    }
}
