using ExpenseManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseManager.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260716151630_AddAuthSessionsAndAccountActions")]
public sealed class AddAuthSessionsAndAccountActions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "token_version",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                revoked_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_refresh_tokens", x => x.id);
                table.ForeignKey(
                    name: "fk_refresh_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_refresh_tokens_refresh_tokens_replaced_by_token_id",
                    column: x => x.replaced_by_token_id,
                    principalTable: "refresh_tokens",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "account_verification_codes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                pending_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                failed_attempts = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_account_verification_codes", x => x.id);
                table.ForeignKey(
                    name: "fk_account_verification_codes_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_users_token_version",
            table: "users",
            column: "token_version");
        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_replaced_by_token_id",
            table: "refresh_tokens",
            column: "replaced_by_token_id");
        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_token_hash",
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_user_id_expires_at",
            table: "refresh_tokens",
            columns: new[] { "user_id", "expires_at" });
        migrationBuilder.CreateIndex(
            name: "ix_account_verification_codes_user_id_purpose_created_at",
            table: "account_verification_codes",
            columns: new[] { "user_id", "purpose", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "account_verification_codes");
        migrationBuilder.DropTable(name: "refresh_tokens");
        migrationBuilder.DropIndex(name: "ix_users_token_version", table: "users");
        migrationBuilder.DropColumn(name: "token_version", table: "users");
    }
}
