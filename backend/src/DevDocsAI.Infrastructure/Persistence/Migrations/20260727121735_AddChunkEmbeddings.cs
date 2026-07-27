using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DevDocsAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chunk_embeddings",
                columns: table => new
                {
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_embeddings", x => x.ChunkId);
                    table.ForeignKey(
                        name: "FK_chunk_embeddings_document_chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalTable: "document_chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_embeddings_DocumentId",
                table: "chunk_embeddings",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_chunk_embeddings_Embedding",
                table: "chunk_embeddings",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_embeddings_ProjectId",
                table: "chunk_embeddings",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunk_embeddings");
        }
    }
}
