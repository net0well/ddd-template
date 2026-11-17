using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Outbox_And_Auditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "created_at",
                schema: "public",
                table: "todo_items",
                newName: "created_on_utc");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "public",
                table: "users",
                type: "uuid",
                nullable: true);

            // Existing users get the migration time (their creation was never recorded); new rows are stamped by
            // the application, so the default is dropped right after.
            migrationBuilder.AddColumn<DateTime>(
                name: "created_on_utc",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.Sql("ALTER TABLE public.users ALTER COLUMN created_on_utc DROP DEFAULT;");

            migrationBuilder.AddColumn<Guid>(
                name: "modified_by",
                schema: "public",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on_utc",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "public",
                table: "todo_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "modified_by",
                schema: "public",
                table: "todo_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "modified_on_utc",
                schema: "public",
                table: "todo_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "public",
                table: "refresh_tokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trace_parent = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dead_lettered_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message_consumers",
                schema: "public",
                columns: table => new
                {
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message_consumers", x => new { x.outbox_message_id, x.handler });
                    table.ForeignKey(
                        name: "fk_outbox_message_consumers_outbox_messages_outbox_message_id",
                        column: x => x.outbox_message_id,
                        principalSchema: "public",
                        principalTable: "outbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_dead_lettered",
                schema: "public",
                table: "outbox_messages",
                column: "dead_lettered_on_utc",
                filter: "dead_lettered_on_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "public",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed",
                schema: "public",
                table: "outbox_messages",
                column: "processed_on_utc",
                filter: "processed_on_utc IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message_consumers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_on_utc",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "modified_by",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "modified_on_utc",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "public",
                table: "todo_items");

            migrationBuilder.DropColumn(
                name: "modified_by",
                schema: "public",
                table: "todo_items");

            migrationBuilder.DropColumn(
                name: "modified_on_utc",
                schema: "public",
                table: "todo_items");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.RenameColumn(
                name: "created_on_utc",
                schema: "public",
                table: "todo_items",
                newName: "created_at");
        }
    }
}
