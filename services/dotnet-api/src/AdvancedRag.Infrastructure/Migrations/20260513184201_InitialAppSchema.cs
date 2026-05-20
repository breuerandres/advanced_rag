using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAppSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: false),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    current_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_draft_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_published_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_ai_budget_limits",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monthly_budget_usd = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_ai_budget_limits", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_ai_budget_limits_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_ai_budget_limits_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_groups",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_groups", x => new { x.user_id, x.group_id });
                    table.ForeignKey(
                        name: "FK_user_groups_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "app",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_groups_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "app",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_permissions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attribute_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attribute_value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_permissions_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "app",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_permissions_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_tags",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_tags_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    document_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    audience = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    content_html = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    submitted_for_review_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_for_review_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    indexing_job_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_versions_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_versions_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_versions_users_submitted_for_review_by_user_id",
                        column: x => x.submitted_for_review_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "viewer_exchange_codes",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    allowed_statuses = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_exchange_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_viewer_exchange_codes_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_viewer_exchange_codes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "viewer_token_audit",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer_token_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_token_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_viewer_token_audit_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_viewer_token_audit_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_metadata",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_filename = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    imported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_metadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_metadata_document_versions_document_version_id",
                        column: x => x.document_version_id,
                        principalSchema: "app",
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_import_metadata_users_imported_by_user_id",
                        column: x => x.imported_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_comments",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_comments_document_versions_document_version_id",
                        column: x => x.document_version_id,
                        principalSchema: "app",
                        principalTable: "document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_comments_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_actor_user_id",
                schema: "app",
                table: "audit_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_created_at",
                schema: "app",
                table: "audit_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_event_type",
                schema: "app",
                table: "audit_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "IX_groups_name",
                schema: "app",
                table: "groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_metadata_imported_by_user_id",
                schema: "app",
                table: "import_metadata",
                column: "imported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_metadata_document_version_id",
                schema: "app",
                table: "import_metadata",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_permissions_attribute_key_attribute_value",
                schema: "app",
                table: "document_permissions",
                columns: new[] { "attribute_key", "attribute_value" });

            migrationBuilder.CreateIndex(
                name: "IX_document_permissions_group_id",
                schema: "app",
                table: "document_permissions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_permissions_document_id",
                schema: "app",
                table: "document_permissions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_document_id_name",
                schema: "app",
                table: "document_tags",
                columns: new[] { "document_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_document_id_state",
                schema: "app",
                table: "document_versions",
                columns: new[] { "document_id", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_document_id_version_number",
                schema: "app",
                table: "document_versions",
                columns: new[] { "document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_published_by_user_id",
                schema: "app",
                table: "document_versions",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_submitted_for_review_by_user_id",
                schema: "app",
                table: "document_versions",
                column: "submitted_for_review_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_created_by_user_id",
                schema: "app",
                table: "documents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_current_state",
                schema: "app",
                table: "documents",
                column: "current_state");

            migrationBuilder.CreateIndex(
                name: "IX_review_comments_actor_user_id",
                schema: "app",
                table: "review_comments",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_comments_document_version_id",
                schema: "app",
                table: "review_comments",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                schema: "app",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_ai_budget_limits_updated_by_user_id",
                schema: "app",
                table: "user_ai_budget_limits",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_groups_group_id",
                schema: "app",
                table: "user_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "app",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                schema: "app",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_viewer_exchange_codes_code_hash",
                schema: "app",
                table: "viewer_exchange_codes",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_viewer_exchange_codes_expires_at",
                schema: "app",
                table: "viewer_exchange_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_viewer_exchange_codes_document_id",
                schema: "app",
                table: "viewer_exchange_codes",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_viewer_exchange_codes_user_id",
                schema: "app",
                table: "viewer_exchange_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_viewer_token_audit_document_id",
                schema: "app",
                table: "viewer_token_audit",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_viewer_token_audit_user_id",
                schema: "app",
                table: "viewer_token_audit",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_viewer_token_audit_viewer_token_id",
                schema: "app",
                table: "viewer_token_audit",
                column: "viewer_token_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "import_metadata",
                schema: "app");

            migrationBuilder.DropTable(
                name: "document_permissions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "document_tags",
                schema: "app");

            migrationBuilder.DropTable(
                name: "review_comments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_ai_budget_limits",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_groups",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "viewer_exchange_codes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "viewer_token_audit",
                schema: "app");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "app");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");
        }
    }
}
