using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

public partial class AddDocumentVersionIndexingStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.document_versions') is not null
                   and not exists(
                       select 1
                       from information_schema.columns
                       where table_schema = 'app'
                         and table_name = 'document_versions'
                         and column_name = 'indexing_status'
                   ) then
                    alter table app.document_versions
                    add column indexing_status character varying(32) not null default 'None';
                elsif to_regclass('app.instruction_versions') is not null
                   and not exists(
                       select 1
                       from information_schema.columns
                       where table_schema = 'app'
                         and table_name = 'instruction_versions'
                         and column_name = 'indexing_status'
                   ) then
                    alter table app.instruction_versions
                    add column indexing_status character varying(32) not null default 'None';
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
                if exists(
                    select 1
                    from information_schema.columns
                    where table_schema = 'app'
                      and table_name = 'document_versions'
                      and column_name = 'indexing_status'
                ) then
                    alter table app.document_versions drop column indexing_status;
                elsif exists(
                    select 1
                    from information_schema.columns
                    where table_schema = 'app'
                      and table_name = 'instruction_versions'
                      and column_name = 'indexing_status'
                ) then
                    alter table app.instruction_versions drop column indexing_status;
                end if;
            end $$;
            """);
    }
}
