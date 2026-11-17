using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Refactor_Domain_Model : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refresh tokens: store only the hash. Existing tokens are hashed in place (same format as
            // TokenProvider.HashRefreshToken: uppercase hex SHA-256), so current sessions keep working.
            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_on_utc",
                schema: "public",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "replaced_by_token_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "revoked_on_utc",
                schema: "public",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE public.refresh_tokens SET token_hash = upper(encode(sha256(convert_to(token, 'UTF8')), 'hex'));");

            migrationBuilder.AlterColumn<string>(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            // Users: every existing user becomes a Member; e-mails are normalized like the Email value object.
            migrationBuilder.AddColumn<List<string>>(
                name: "roles",
                schema: "public",
                table: "users",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{Member}'");

            migrationBuilder.Sql("UPDATE public.users SET email = lower(trim(email));");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "public",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "public",
                table: "todo_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hashes cannot be turned back into tokens: sessions are dropped on rollback.
            migrationBuilder.Sql("DELETE FROM public.refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "roles",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_on_utc",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "replaced_by_token_id",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "revoked_on_utc",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "public",
                table: "todo_items",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "token",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens",
                column: "token",
                unique: true);
        }
    }
}
