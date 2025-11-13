using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SourceCodeSummariser.Migrations
{
    /// <inheritdoc />
    public partial class AddTagsAndEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Embedding column to Members table
            migrationBuilder.AddColumn<byte[]>(
                name: "Embedding",
                table: "Members",
                type: "BLOB",
                nullable: true);

            // Create Tags table
            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            // Create MemberTags junction table
            migrationBuilder.CreateTable(
                name: "MemberTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberTags_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create unique index on Tags (Name, Category)
            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name_Category",
                table: "Tags",
                columns: new[] { "Name", "Category" },
                unique: true);

            // Create index on MemberTags (MemberId)
            migrationBuilder.CreateIndex(
                name: "IX_MemberTags_MemberId",
                table: "MemberTags",
                column: "MemberId");

            // Create index on MemberTags (TagId)
            migrationBuilder.CreateIndex(
                name: "IX_MemberTags_TagId",
                table: "MemberTags",
                column: "TagId");

            // Create unique index on MemberTags (MemberId, TagId)
            migrationBuilder.CreateIndex(
                name: "IX_MemberTags_MemberId_TagId",
                table: "MemberTags",
                columns: new[] { "MemberId", "TagId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop MemberTags table
            migrationBuilder.DropTable(
                name: "MemberTags");

            // Drop Tags table
            migrationBuilder.DropTable(
                name: "Tags");

            // Drop Embedding column
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Members");
        }
    }
}
