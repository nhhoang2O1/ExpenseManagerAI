using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260716151629_AddConcurrencyGoalHistoryAndIdempotency")]
public sealed class AddConcurrencyGoalHistoryAndIdempotency : Migration
{
    private static readonly string[] VersionedTables =
        ["users", "categories", "transactions", "budgets", "goals", "reminders"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in VersionedTables)
        {
            migrationBuilder.AddColumn<long>(
                name: "version",
                table: table,
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
        }

        migrationBuilder.AddColumn<decimal>(
            name: "requested_amount",
            table: "goal_histories",
            type: "numeric(18,0)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "balance_after",
            table: "goal_histories",
            type: "numeric(18,0)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                status_code = table.Column<int>(type: "integer", nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => x.id);
                table.ForeignKey(
                    name: "fk_idempotency_records_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_expires_at",
            table: "idempotency_records",
            column: "expires_at");
        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_user_id_scope_key",
            table: "idempotency_records",
            columns: new[] { "user_id", "scope", "key" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_transactions_user_id_transaction_date_created_at_id",
            table: "transactions",
            columns: new[] { "user_id", "transaction_date", "created_at", "id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "idempotency_records");
        migrationBuilder.DropIndex(
            name: "ix_transactions_user_id_transaction_date_created_at_id",
            table: "transactions");
        migrationBuilder.DropColumn(name: "requested_amount", table: "goal_histories");
        migrationBuilder.DropColumn(name: "balance_after", table: "goal_histories");
        foreach (var table in VersionedTables)
            migrationBuilder.DropColumn(name: "version", table: table);
    }
}
