using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstateAggregator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MyListingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommHistoryEntries_MyListings_MyListingId",
                        column: x => x.MyListingId,
                        principalTable: "MyListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommHistoryEntries_CreatedAt",
                table: "CommHistoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommHistoryEntries_MyListingId",
                table: "CommHistoryEntries",
                column: "MyListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommHistoryEntries");
        }
    }
}
