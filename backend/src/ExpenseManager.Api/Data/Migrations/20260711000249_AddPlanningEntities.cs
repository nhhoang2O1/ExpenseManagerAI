using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS budgets (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    category_id uuid NOT NULL,
                    amount numeric(18,0) NOT NULL,
                    month_year character varying(7) NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_budgets PRIMARY KEY (id),
                    CONSTRAINT fk_budgets_categories_category_id FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE RESTRICT,
                    CONSTRAINT fk_budgets_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS goals (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    name character varying(200) NOT NULL,
                    target_amount numeric(18,0) NOT NULL,
                    current_amount numeric(18,0) NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_goals PRIMARY KEY (id),
                    CONSTRAINT fk_goals_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS reminders (
                    id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    content character varying(500) NOT NULL,
                    day_of_month integer NOT NULL,
                    hour integer NOT NULL,
                    minute integer NOT NULL,
                    is_active boolean NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT pk_reminders PRIMARY KEY (id),
                    CONSTRAINT fk_reminders_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS goal_histories (
                    id uuid NOT NULL,
                    goal_id uuid NOT NULL,
                    amount_added numeric(18,0) NOT NULL,
                    date timestamp with time zone NOT NULL,
                    CONSTRAINT pk_goal_histories PRIMARY KEY (id),
                    CONSTRAINT fk_goal_histories_goals_goal_id FOREIGN KEY (goal_id) REFERENCES goals (id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_budgets_category_id ON budgets (category_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ix_budgets_user_id_category_id_month_year ON budgets (user_id, category_id, month_year);
                CREATE INDEX IF NOT EXISTS ix_goal_histories_goal_id_date ON goal_histories (goal_id, date);
                CREATE INDEX IF NOT EXISTS ix_goals_user_id_name ON goals (user_id, name);
                CREATE INDEX IF NOT EXISTS ix_reminders_user_id ON reminders (user_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "budgets");
            migrationBuilder.DropTable(name: "goal_histories");
            migrationBuilder.DropTable(name: "reminders");
            migrationBuilder.DropTable(name: "goals");
        }
    }
}
