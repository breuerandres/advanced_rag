using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260520190000_RenameLegacyAuditEventsToDocuments")]
public partial class RenameLegacyAuditEventsToDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.audit_events') is null then
                    return;
                end if;

            update app.audit_events
            set
                event_type = regexp_replace(event_type, '^instruction\.', 'document.'),
                entity_type = case when entity_type = 'instruction' then 'document' else entity_type end,
                details_json = case
                    when details_json ? 'instructionId'
                        then (details_json - 'instructionId')
                            || jsonb_build_object('documentId', details_json -> 'instructionId')
                    else details_json
                end
            where event_type like 'instruction.%'
               or entity_type = 'instruction'
               or details_json ? 'instructionId';
            end $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.audit_events') is null then
                    return;
                end if;

            update app.audit_events
            set
                event_type = regexp_replace(event_type, '^document\.', 'instruction.'),
                entity_type = case when entity_type = 'document' then 'instruction' else entity_type end,
                details_json = case
                    when details_json ? 'documentId'
                        then (details_json - 'documentId')
                            || jsonb_build_object('instructionId', details_json -> 'documentId')
                    else details_json
                end
            where event_type like 'document.%'
               or entity_type = 'document'
               or details_json ? 'documentId';
            end $$;
            """);
    }
}
