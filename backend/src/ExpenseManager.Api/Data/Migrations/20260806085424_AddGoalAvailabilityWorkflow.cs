using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalAvailabilityWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories");

            migrationBuilder.AddColumn<Guid>(
                name: "goal_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "goals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "goals",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ACTIVE");

            migrationBuilder.AddColumn<string>(
                name: "action_type",
                table: "goal_histories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FUND");

            migrationBuilder.Sql(
                "UPDATE goals SET status = 'READY_TO_COMPLETE' WHERE current_amount = target_amount;");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_goal_id",
                table: "transactions",
                column: "goal_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_goals_status",
                table: "goals",
                sql: "status IN ('ACTIVE', 'READY_TO_COMPLETE', 'COMPLETED', 'CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_histories_action",
                table: "goal_histories",
                sql: "action_type IN ('FUND', 'COMPLETE', 'CANCEL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories",
                sql: "((action_type = 'FUND' AND amount_added > 0) OR (action_type IN ('COMPLETE', 'CANCEL') AND amount_added = 0)) AND (requested_amount IS NULL OR requested_amount > 0) AND (balance_after IS NULL OR balance_after >= 0)");

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_goals_goal_id",
                table: "transactions",
                column: "goal_id",
                principalTable: "goals",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_goals_goal_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_goal_id",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goals_status",
                table: "goals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_histories_action",
                table: "goal_histories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories");

            migrationBuilder.Sql(
                "DELETE FROM goal_histories WHERE action_type IN ('COMPLETE', 'CANCEL')");

            migrationBuilder.DropColumn(
                name: "goal_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "status",
                table: "goals");

            migrationBuilder.DropColumn(
                name: "action_type",
                table: "goal_histories");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories",
                sql: "amount_added > 0 AND (requested_amount IS NULL OR requested_amount > 0) AND (balance_after IS NULL OR balance_after >= 0)");
        }
    }
}
