using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceGoalOwnershipAndSerializeFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM transactions transaction_row
                        JOIN goals goal_row ON goal_row.id = transaction_row.goal_id
                        WHERE transaction_row.goal_id IS NOT NULL
                          AND transaction_row.user_id <> goal_row.user_id
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce transaction/goal ownership: an existing transaction belongs to a different user than its goal.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_goals_goal_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_goal_id",
                table: "transactions");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_goals_id_user_id",
                table: "goals",
                columns: new[] { "id", "user_id" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transactions_goals_goal_id_user_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_goal_id_user_id",
                table: "transactions");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_goals_id_user_id",
                table: "goals");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_goal_id",
                table: "transactions",
                column: "goal_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_goals_goal_id",
                table: "transactions",
                column: "goal_id",
                principalTable: "goals",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
