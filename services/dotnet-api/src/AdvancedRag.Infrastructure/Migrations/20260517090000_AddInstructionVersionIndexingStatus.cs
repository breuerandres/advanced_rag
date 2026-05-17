using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdvancedRag.Infrastructure.Migrations;

public partial class AddInstructionVersionIndexingStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "indexing_status",
            schema: "app",
            table: "instruction_versions",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "None");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "indexing_status",
            schema: "app",
            table: "instruction_versions");
    }
}
