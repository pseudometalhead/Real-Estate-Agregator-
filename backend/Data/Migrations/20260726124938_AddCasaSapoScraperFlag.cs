using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstateAggregator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCasaSapoScraperFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ScrapeCasaSapoEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScrapeCasaSapoEnabled",
                table: "AppSettings");
        }
    }
}
