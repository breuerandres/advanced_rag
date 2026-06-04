using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260604090000_AddViewerSessionHandoffCodes")]
public partial class AddViewerSessionHandoffCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.users') is null or to_regclass('app.documents') is null then
                    return;
                end if;

                create table if not exists app.viewer_session_handoff_codes (
                    "Id"                  uuid primary key,
                    code_hash             varchar(64) not null,
                    user_id               uuid not null references app.users("Id") on delete restrict,
                    document_id           uuid not null references app.documents("Id") on delete cascade,
                    purpose               varchar(32) not null,
                    allowed_state_scope   varchar(120) not null,
                    expires_at            timestamptz not null,
                    consumed_at           timestamptz,
                    created_at            timestamptz not null default now(),
                    request_id            varchar(128) not null
                );

                create unique index if not exists "IX_viewer_session_handoff_codes_code_hash"
                    on app.viewer_session_handoff_codes (code_hash);

                create index if not exists "IX_viewer_session_handoff_codes_expires_at"
                    on app.viewer_session_handoff_codes (expires_at);

                create index if not exists "IX_viewer_session_handoff_codes_user_id"
                    on app.viewer_session_handoff_codes (user_id);

                create index if not exists "IX_viewer_session_handoff_codes_document_id"
                    on app.viewer_session_handoff_codes (document_id);
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists app.viewer_session_handoff_codes;");
    }
}
