using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260615130000_GrantReportingReaderUsersReadAccess")]
public partial class GrantReportingReaderUsersReadAccess : Migration
{
    // The feedback reporting query (NpgsqlFeedbackReportingService) joins rag.v_query_audit_with_citations
    // to app.users to surface the reviewer's name and email instead of the raw user id. The reporting role
    // (app_reporting_reader) only has access to the rag reporting views, so it needs read access to the user
    // directory. We grant column-level SELECT on Id/display_name/email ONLY -- never password_hash or other
    // sensitive columns -- keeping the reporting role least-privileged.
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regrole('app_reporting_reader') is not null then
                    grant usage on schema app to app_reporting_reader;

                    if to_regclass('app.users') is not null then
                        grant select ("Id", display_name, email)
                            on table app.users to app_reporting_reader;
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
                if to_regrole('app_reporting_reader') is not null then
                    if to_regclass('app.users') is not null then
                        revoke select ("Id", display_name, email)
                            on table app.users from app_reporting_reader;
                    end if;
                end if;
            end $$;
            """);
    }
}
