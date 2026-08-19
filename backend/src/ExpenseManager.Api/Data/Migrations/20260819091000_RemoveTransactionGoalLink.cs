using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

public partial class RemoveTransactionGoalLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_transactions_goals_goal_id_user_id",
            table: "transactions");

        migrationBuilder.DropIndex(
            name: "ix_transactions_goal_id_user_id",
            table: "transactions");

        migrationBuilder.DropColumn(
            name: "goal_id",
            table: "transactions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "goal_id",
            table: "transactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_transactions_goal_id_user_id",
            table: "transactions",
            columns: new[] { "goal_id", "user_id" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_transactions_goals_goal_id_user_id",
            table: "transactions",
            columns: new[] { "goal_id", "user_id" },
            principalTable: "goals",
            principalColumns: new[] { "id", "user_id" },
            onDelete: ReferentialAction.Restrict);
    }
}
