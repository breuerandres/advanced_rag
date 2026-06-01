using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260531170000_AddDocumentImages")]
public partial class AddDocumentImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            create table if not exists app.document_images (
                "Id"                uuid primary key,
                document_id         uuid not null references app.documents("Id") on delete cascade,
                object_key          varchar(512) not null,
                original_filename   varchar(260) not null,
                content_type        varchar(120) not null,
                size_bytes          bigint not null,
                sha256_hash         varchar(64) not null,
                alt_text            varchar(500) not null,
                uploaded_by_user_id uuid not null references app.users("Id") on delete restrict,
                created_at          timestamptz not null default now()
            );

            create index if not exists "IX_document_images_document_id"
                on app.document_images (document_id);

            create unique index if not exists "IX_document_images_object_key"
                on app.document_images (object_key);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("drop table if exists app.document_images;");
    }
}
