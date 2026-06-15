using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260615140500_GrantRagOwnerTenantConfigReadAccess")]
public partial class GrantRagOwnerTenantConfigReadAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regrole('rag_owner') is not null then
                    if to_regclass('app.tenant_config') is not null then
                        grant select on table app.tenant_config to rag_owner;
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
                    if to_regclass('app.tenant_config') is not null then
                        revoke select on table app.tenant_config from rag_owner;
                    end if;
                end if;
            end $$;
            """);
    }
}
