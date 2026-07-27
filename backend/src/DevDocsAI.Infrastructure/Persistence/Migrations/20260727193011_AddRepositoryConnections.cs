using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevDocsAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryConnectionId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "repository_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Owner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Repo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FileCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_connections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repository_connections_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_RepositoryConnectionId",
                table: "documents",
                column: "RepositoryConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_repository_connections_ProjectId",
                table: "repository_connections",
                column: "ProjectId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_repository_connections_RepositoryConnectionId",
                table: "documents",
                column: "RepositoryConnectionId",
                principalTable: "repository_connections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_repository_connections_RepositoryConnectionId",
                table: "documents");

            migrationBuilder.DropTable(
                name: "repository_connections");

            migrationBuilder.DropIndex(
                name: "IX_documents_RepositoryConnectionId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "RepositoryConnectionId",
                table: "documents");
        }
    }
}
