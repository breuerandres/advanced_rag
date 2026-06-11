using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260611090000_GrantRagOwnerDocumentsReadAccess")]
public partial class GrantRagOwnerDocumentsReadAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regrole('rag_owner') is not null then
                    if to_regclass('app.documents') is not null then
                        grant select on table app.documents to rag_owner;
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
                    if to_regclass('app.documents') is not null then
                        revoke select on table app.documents from rag_owner;
                    end if;
                end if;
            end $$;
            """);
    }
}
