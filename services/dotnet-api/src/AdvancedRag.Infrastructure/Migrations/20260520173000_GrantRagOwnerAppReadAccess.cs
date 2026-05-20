using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260520173000_GrantRagOwnerAppReadAccess")]
public partial class GrantRagOwnerAppReadAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regrole('rag_owner') is not null then
                    grant usage on schema app to rag_owner;

                    if to_regclass('app.document_permissions') is not null then
                        grant select on table app.document_permissions to rag_owner;
                    end if;

                    if to_regclass('app.user_ai_budget_limits') is not null then
                        grant select on table app.user_ai_budget_limits to rag_owner;
                    end if;
                end if;
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regrole('rag_owner') is not null then
                    if to_regclass('app.user_ai_budget_limits') is not null then
                        revoke select on table app.user_ai_budget_limits from rag_owner;
                    end if;

                    if to_regclass('app.document_permissions') is not null then
                        revoke select on table app.document_permissions from rag_owner;
                    end if;
                end if;
            end $$;
            """);
    }
}
