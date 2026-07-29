using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728120000_AddEmailVerification")]
public sealed class AddEmailVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing accounts predate verification and must stay usable.
        migrationBuilder.AddColumn<bool>(
            name: "is_email_verified",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "is_email_verified", table: "users");
}
