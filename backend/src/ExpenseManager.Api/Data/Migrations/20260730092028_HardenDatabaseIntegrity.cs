using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenDatabaseIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Do not silently rewrite financial records or cross-user links. A failed
            // preflight leaves the migration transaction untouched and identifies the
            // data category that must be corrected before retrying.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM users
                        GROUP BY lower(email)
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot normalize users.email: duplicate addresses differ only by letter case.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM transactions transaction_row
                        JOIN categories category_row ON category_row.id = transaction_row.category_id
                        WHERE transaction_row.user_id <> category_row.user_id
                           OR transaction_row.type <> category_row.type
                    ) THEN
                        RAISE EXCEPTION 'Cannot add transaction/category integrity constraint: an existing transaction has a different owner or type from its category.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM budgets budget_row
                        JOIN categories category_row ON category_row.id = budget_row.category_id
                        WHERE budget_row.user_id <> category_row.user_id
                    ) THEN
                        RAISE EXCEPTION 'Cannot add budget/category integrity constraint: an existing budget belongs to a different user than its category.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM transactions transaction_row
                        JOIN receipts receipt_row ON receipt_row.id = transaction_row.receipt_id
                        WHERE transaction_row.user_id <> receipt_row.user_id
                    ) THEN
                        RAISE EXCEPTION 'Cannot add transaction/receipt integrity constraint: an existing transaction belongs to a different user than its receipt.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM transactions WHERE amount <= 0
                        UNION ALL SELECT 1 FROM transactions WHERE type NOT IN ('INCOME', 'EXPENSE')
                        UNION ALL SELECT 1 FROM budgets WHERE amount <= 0
                        UNION ALL SELECT 1 FROM budgets WHERE month_year !~ '^\\d{4}-(0[1-9]|1[0-2])$'
                        UNION ALL SELECT 1 FROM goals WHERE target_amount <= 0 OR current_amount < 0 OR current_amount > target_amount
                        UNION ALL SELECT 1 FROM goal_histories WHERE amount_added <= 0 OR requested_amount <= 0 OR balance_after < 0
                        UNION ALL SELECT 1 FROM reminders WHERE day_of_month NOT BETWEEN 1 AND 31 OR hour NOT BETWEEN 0 AND 23 OR minute NOT BETWEEN 0 AND 59
                        UNION ALL SELECT 1 FROM receipts WHERE file_size <= 0 OR processing_attempts < 0
                        UNION ALL SELECT 1 FROM ocr_results WHERE overall_confidence NOT BETWEEN 0 AND 1 OR total_amount <= 0 OR vat_amount < 0 OR vat_amount > total_amount
                        UNION ALL SELECT 1 FROM categories WHERE type NOT IN ('INCOME', 'EXPENSE')
                    ) THEN
                        RAISE EXCEPTION 'Cannot add database check constraints: existing rows contain invalid business values.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("UPDATE users SET email = lower(email) WHERE email <> lower(email);");

            migrationBuilder.DropForeignKey(
                name: "fk_budgets_categories_category_id",
                table: "budgets");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_categories_category_id",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_receipts_receipt_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_users_token_version",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_transactions_category_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_user_id_transaction_date",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_budgets_category_id",
                table: "budgets");

            migrationBuilder.AlterColumn<bool>(
                name: "is_email_verified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<long>(
                name: "amount",
                table: "transactions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)");

            migrationBuilder.AlterColumn<long>(
                name: "vat_amount",
                table: "ocr_results",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "total_amount",
                table: "ocr_results",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "target_amount",
                table: "goals",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)");

            migrationBuilder.AlterColumn<long>(
                name: "current_amount",
                table: "goals",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)");

            migrationBuilder.AlterColumn<long>(
                name: "requested_amount",
                table: "goal_histories",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "balance_after",
                table: "goal_histories",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "amount_added",
                table: "goal_histories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)");

            migrationBuilder.AlterColumn<long>(
                name: "amount",
                table: "budgets",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,0)");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_receipts_id_user_id",
                table: "receipts",
                columns: new[] { "id", "user_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_categories_id_user_id",
                table: "categories",
                columns: new[] { "id", "user_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_categories_id_user_id_type",
                table: "categories",
                columns: new[] { "id", "user_id", "type" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_email_lowercase",
                table: "users",
                sql: "email = lower(email)");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id_user_id_type",
                table: "transactions",
                columns: new[] { "category_id", "user_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_receipt_id_user_id",
                table: "transactions",
                columns: new[] { "receipt_id", "user_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_amount_positive",
                table: "transactions",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_type",
                table: "transactions",
                sql: "type IN ('INCOME', 'EXPENSE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reminders_schedule",
                table: "reminders",
                sql: "day_of_month BETWEEN 1 AND 31 AND hour BETWEEN 0 AND 23 AND minute BETWEEN 0 AND 59");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_file_size_positive",
                table: "receipts",
                sql: "file_size > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipts_processing_attempts",
                table: "receipts",
                sql: "processing_attempts >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ocr_results_amounts_and_confidence",
                table: "ocr_results",
                sql: "overall_confidence BETWEEN 0 AND 1 AND (total_amount IS NULL OR total_amount > 0) AND (vat_amount IS NULL OR vat_amount >= 0) AND (total_amount IS NULL OR vat_amount IS NULL OR vat_amount <= total_amount)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goals_amounts",
                table: "goals",
                sql: "target_amount > 0 AND current_amount >= 0 AND current_amount <= target_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories",
                sql: "amount_added > 0 AND (requested_amount IS NULL OR requested_amount > 0) AND (balance_after IS NULL OR balance_after >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_categories_type",
                table: "categories",
                sql: "type IN ('INCOME', 'EXPENSE')");

            migrationBuilder.CreateIndex(
                name: "ix_budgets_category_id_user_id",
                table: "budgets",
                columns: new[] { "category_id", "user_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_budgets_amount_positive",
                table: "budgets",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_budgets_month_year",
                table: "budgets",
                sql: "month_year ~ '^\\d{4}-(0[1-9]|1[0-2])$'");

            migrationBuilder.AddForeignKey(
                name: "fk_budgets_categories_category_id_user_id",
                table: "budgets",
                columns: new[] { "category_id", "user_id" },
                principalTable: "categories",
                principalColumns: new[] { "id", "user_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_categories_category_id_user_id_type",
                table: "transactions",
                columns: new[] { "category_id", "user_id", "type" },
                principalTable: "categories",
                principalColumns: new[] { "id", "user_id", "type" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_receipts_receipt_id_user_id",
                table: "transactions",
                columns: new[] { "receipt_id", "user_id" },
                principalTable: "receipts",
                principalColumns: new[] { "id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_budgets_categories_category_id_user_id",
                table: "budgets");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_categories_category_id_user_id_type",
                table: "transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_transactions_receipts_receipt_id_user_id",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_email_lowercase",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_transactions_category_id_user_id_type",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "ix_transactions_receipt_id_user_id",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_amount_positive",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_type",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reminders_schedule",
                table: "reminders");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_receipts_id_user_id",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_file_size_positive",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipts_processing_attempts",
                table: "receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ocr_results_amounts_and_confidence",
                table: "ocr_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goals_amounts",
                table: "goals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_goal_histories_amounts",
                table: "goal_histories");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_categories_id_user_id",
                table: "categories");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_categories_id_user_id_type",
                table: "categories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_categories_type",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "ix_budgets_category_id_user_id",
                table: "budgets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budgets_amount_positive",
                table: "budgets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budgets_month_year",
                table: "budgets");

            migrationBuilder.AlterColumn<bool>(
                name: "is_email_verified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "transactions",
                type: "numeric(18,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "vat_amount",
                table: "ocr_results",
                type: "numeric(18,0)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount",
                table: "ocr_results",
                type: "numeric(18,0)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "target_amount",
                table: "goals",
                type: "numeric(18,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "current_amount",
                table: "goals",
                type: "numeric(18,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "requested_amount",
                table: "goal_histories",
                type: "numeric(18,0)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "balance_after",
                table: "goal_histories",
                type: "numeric(18,0)",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount_added",
                table: "goal_histories",
                type: "numeric(18,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "budgets",
                type: "numeric(18,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "ix_users_token_version",
                table: "users",
                column: "token_version");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_transaction_date",
                table: "transactions",
                columns: new[] { "user_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_budgets_category_id",
                table: "budgets",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_budgets_categories_category_id",
                table: "budgets",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_categories_category_id",
                table: "transactions",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_transactions_receipts_receipt_id",
                table: "transactions",
                column: "receipt_id",
                principalTable: "receipts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
