using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260819090000_AddCategoryActiveFlag")]
public partial class AddCategoryActiveFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "is_active", table: "categories", type: "boolean",
            nullable: false, defaultValue: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "is_active", table: "categories");
}
