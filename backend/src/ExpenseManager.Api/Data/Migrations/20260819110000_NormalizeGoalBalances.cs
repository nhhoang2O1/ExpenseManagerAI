using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

public partial class NormalizeGoalBalances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE goals SET status = 'COMPLETED' WHERE current_amount >= target_amount AND status IN ('READY_TO_COMPLETE', 'ACTIVE');");
        migrationBuilder.Sql("UPDATE goals SET status = 'ACTIVE' WHERE current_amount < target_amount AND status = 'COMPLETED';");
        migrationBuilder.DropCheckConstraint("ck_goals_amounts", "goals");
        migrationBuilder.AddCheckConstraint("ck_goals_amounts", "goals", "target_amount > 0");
        migrationBuilder.DropCheckConstraint("ck_goal_histories_amounts", "goal_histories");
        migrationBuilder.DropCheckConstraint("ck_goal_histories_action", "goal_histories");
        migrationBuilder.AddCheckConstraint("ck_goal_histories_amounts", "goal_histories", "((action_type = 'FUND' AND amount_added > 0) OR (action_type = 'WITHDRAW' AND amount_added < 0) OR (action_type IN ('COMPLETE', 'CANCEL') AND amount_added = 0))");
        migrationBuilder.AddCheckConstraint("ck_goal_histories_action", "goal_histories", "action_type IN ('FUND', 'WITHDRAW', 'COMPLETE', 'CANCEL')");
        migrationBuilder.DropColumn("current_amount", "goals");
        migrationBuilder.DropColumn("completed_at", "goals");
        migrationBuilder.DropColumn("requested_amount", "goal_histories");
        migrationBuilder.DropColumn("balance_after", "goal_histories");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>("current_amount", "goals", "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateTime>("completed_at", "goals", "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<long>("requested_amount", "goal_histories", "bigint", nullable: true);
        migrationBuilder.AddColumn<long>("balance_after", "goal_histories", "bigint", nullable: true);
        migrationBuilder.DropCheckConstraint("ck_goals_amounts", "goals");
        migrationBuilder.AddCheckConstraint("ck_goals_amounts", "goals", "target_amount > 0 AND current_amount >= 0 AND current_amount <= target_amount");
        migrationBuilder.DropCheckConstraint("ck_goal_histories_amounts", "goal_histories");
        migrationBuilder.DropCheckConstraint("ck_goal_histories_action", "goal_histories");
        migrationBuilder.AddCheckConstraint("ck_goal_histories_amounts", "goal_histories", "((action_type = 'FUND' AND amount_added > 0) OR (action_type IN ('COMPLETE', 'CANCEL') AND amount_added = 0)) AND (requested_amount IS NULL OR requested_amount > 0) AND (balance_after IS NULL OR balance_after >= 0)");
        migrationBuilder.AddCheckConstraint("ck_goal_histories_action", "goal_histories", "action_type IN ('FUND', 'COMPLETE', 'CANCEL')");
    }
}
