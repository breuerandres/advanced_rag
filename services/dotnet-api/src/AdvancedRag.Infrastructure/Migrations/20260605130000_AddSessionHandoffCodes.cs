using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260605130000_AddSessionHandoffCodes")]
public partial class AddSessionHandoffCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.users') is null then
                    return;
                end if;

                create table if not exists app.session_handoff_codes (
                    "Id"                  uuid primary key,
                    code_hash             varchar(64) not null,
                    user_id               uuid not null references app.users("Id") on delete restrict,
                    target                varchar(16) not null,
                    expires_at            timestamptz not null,
                    consumed_at           timestamptz,
                    created_at            timestamptz not null default now(),
                    request_id            varchar(128) not null
                );

                create unique index if not exists "IX_session_handoff_codes_code_hash"
                    on app.session_handoff_codes (code_hash);

                create index if not exists "IX_session_handoff_codes_expires_at"
                    on app.session_handoff_codes (expires_at);

                create index if not exists "IX_session_handoff_codes_user_id"
                    on app.session_handoff_codes (user_id);
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists app.session_handoff_codes;");
    }
}
