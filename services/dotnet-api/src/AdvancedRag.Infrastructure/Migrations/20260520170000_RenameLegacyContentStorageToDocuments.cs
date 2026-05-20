using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

public partial class RenameLegacyContentStorageToDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            do $$
            begin
                if to_regclass('app.instructions') is not null and to_regclass('app.documents') is null then
                    alter table app.instructions rename to documents;
                end if;
                if to_regclass('app.instruction_versions') is not null and to_regclass('app.document_versions') is null then
                    alter table app.instruction_versions rename to document_versions;
                end if;
                if to_regclass('app.instruction_permissions') is not null and to_regclass('app.document_permissions') is null then
                    alter table app.instruction_permissions rename to document_permissions;
                end if;
                if to_regclass('app.instruction_tags') is not null and to_regclass('app.document_tags') is null then
                    alter table app.instruction_tags rename to document_tags;
                end if;

                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_versions' and column_name = 'instruction_id') then
                    alter table app.document_versions rename column instruction_id to document_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_versions' and column_name = 'instruction_type') then
                    alter table app.document_versions rename column instruction_type to document_type;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_permissions' and column_name = 'instruction_id') then
                    alter table app.document_permissions rename column instruction_id to document_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_tags' and column_name = 'instruction_id') then
                    alter table app.document_tags rename column instruction_id to document_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'review_comments' and column_name = 'instruction_version_id') then
                    alter table app.review_comments rename column instruction_version_id to document_version_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'import_metadata' and column_name = 'instruction_version_id') then
                    alter table app.import_metadata rename column instruction_version_id to document_version_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'viewer_exchange_codes' and column_name = 'instruction_id') then
                    alter table app.viewer_exchange_codes rename column instruction_id to document_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'viewer_token_audit' and column_name = 'instruction_id') then
                    alter table app.viewer_token_audit rename column instruction_id to document_id;
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
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'viewer_token_audit' and column_name = 'document_id') then
                    alter table app.viewer_token_audit rename column document_id to instruction_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'viewer_exchange_codes' and column_name = 'document_id') then
                    alter table app.viewer_exchange_codes rename column document_id to instruction_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'import_metadata' and column_name = 'document_version_id') then
                    alter table app.import_metadata rename column document_version_id to instruction_version_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'review_comments' and column_name = 'document_version_id') then
                    alter table app.review_comments rename column document_version_id to instruction_version_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_tags' and column_name = 'document_id') then
                    alter table app.document_tags rename column document_id to instruction_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_permissions' and column_name = 'document_id') then
                    alter table app.document_permissions rename column document_id to instruction_id;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_versions' and column_name = 'document_type') then
                    alter table app.document_versions rename column document_type to instruction_type;
                end if;
                if exists(select 1 from information_schema.columns where table_schema = 'app' and table_name = 'document_versions' and column_name = 'document_id') then
                    alter table app.document_versions rename column document_id to instruction_id;
                end if;

                if to_regclass('app.document_tags') is not null and to_regclass('app.instruction_tags') is null then
                    alter table app.document_tags rename to instruction_tags;
                end if;
                if to_regclass('app.document_permissions') is not null and to_regclass('app.instruction_permissions') is null then
                    alter table app.document_permissions rename to instruction_permissions;
                end if;
                if to_regclass('app.document_versions') is not null and to_regclass('app.instruction_versions') is null then
                    alter table app.document_versions rename to instruction_versions;
                end if;
                if to_regclass('app.documents') is not null and to_regclass('app.instructions') is null then
                    alter table app.documents rename to instructions;
                end if;
            end $$;
            """);
    }
}
