using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ExpenseManager.Api.Data;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260817090000_AddFinancialCycleStartDay")]
public partial class AddFinancialCycleStartDay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "financial_cycle_start_day",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddCheckConstraint(
            name: "ck_users_financial_cycle_start_day",
            table: "users",
            sql: "financial_cycle_start_day BETWEEN 1 AND 31");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_users_financial_cycle_start_day",
            table: "users");

        migrationBuilder.DropColumn(
            name: "financial_cycle_start_day",
            table: "users");
    }
}
