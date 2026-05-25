using System.Security.Cryptography;
using System.Text;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260522134000_SeedDefaultAdminUser")]
public partial class SeedDefaultAdminUser : Migration
{
    private const string AdminUserId = "10000000-0000-0000-0000-000000000001";
    private const string AdminRoleId = "00000000-0000-0000-0000-0000000000a1";
    private const string DocumentManagerRoleId = "00000000-0000-0000-0000-0000000000a2";
    private const string ViewerRoleId = "00000000-0000-0000-0000-0000000000a3";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        string passwordHash = SqlLiteral(CreateDefaultAdminPasswordHash());

        migrationBuilder.Sql(
            $"""
            do $$
            begin
                if to_regclass('app.roles') is null
                    or to_regclass('app.users') is null
                    or to_regclass('app.user_roles') is null
                    or to_regclass('app.user_ai_budget_limits') is null then
                    return;
                end if;

            insert into app.roles ("Id", name) values
                ('{AdminRoleId}', 'Admin'),
                ('{DocumentManagerRoleId}', 'DocumentManager'),
                ('{ViewerRoleId}', 'Viewer')
            on conflict (name) do nothing;

            insert into app.users ("Id", email, display_name, password_hash, is_active, created_at)
            values (
                '{AdminUserId}',
                'admin@admin.com',
                'Default Admin',
                {passwordHash},
                true,
                now()
            )
            on conflict (email) do update set
                display_name = excluded.display_name,
                password_hash = excluded.password_hash,
                is_active = true;

            insert into app.user_roles (user_id, role_id)
            select users."Id", roles."Id"
            from app.users users
            join app.roles roles on roles.name = 'Admin'
            where users.email = 'admin@admin.com'
            on conflict do nothing;

            insert into app.user_ai_budget_limits (
                user_id,
                monthly_budget_usd,
                is_disabled,
                updated_at,
                updated_by_user_id
            )
            select
                users."Id",
                5.00,
                false,
                now(),
                users."Id"
            from app.users users
            where users.email = 'admin@admin.com'
            on conflict (user_id) do update set
                monthly_budget_usd = coalesce(app.user_ai_budget_limits.monthly_budget_usd, excluded.monthly_budget_usd),
                is_disabled = false,
                updated_at = now();
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.users') is null then
                    return;
                end if;

            delete from app.user_ai_budget_limits
            where user_id in (select "Id" from app.users where email = 'admin@admin.com');

            delete from app.user_roles
            where user_id in (select "Id" from app.users where email = 'admin@admin.com');

            delete from app.users
            where email = 'admin@admin.com'
              and "Id" = '10000000-0000-0000-0000-000000000001';
            end $$;
            """);
    }

    private static string CreateDefaultAdminPasswordHash()
    {
        byte[] salt = Encoding.UTF8.GetBytes("advanced-rag-default-admin");
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            "admin",
            salt,
            210_000,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static string SqlLiteral(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
