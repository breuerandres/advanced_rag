using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260615140000_AddTenantConfigOperationalFields")]
public partial class AddTenantConfigOperationalFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            alter table app.tenant_config
                add column if not exists customer_timezone       text    not null default 'America/Argentina/Buenos_Aires',
                add column if not exists import_max_file_size_mb  int     not null default 10,
                add column if not exists chat_max_question_chars  int     not null default 4000,
                add column if not exists seeded_from_env          boolean not null default false;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            alter table app.tenant_config
                drop column if exists customer_timezone,
                drop column if exists import_max_file_size_mb,
                drop column if exists chat_max_question_chars,
                drop column if exists seeded_from_env;
            """);
    }
}
