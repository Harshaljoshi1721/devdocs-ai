using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevDocsAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Iterations = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_runs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_runs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tool_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    OutputJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_executions_agent_runs_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "agent_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_ProjectId",
                table: "agent_runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_ProjectId_UserId",
                table: "agent_runs",
                columns: new[] { "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_UserId",
                table: "agent_runs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_executions_AgentRunId",
                table: "tool_executions",
                column: "AgentRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_executions");

            migrationBuilder.DropTable(
                name: "agent_runs");
        }
    }
}
