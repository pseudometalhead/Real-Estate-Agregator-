using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstateAggregator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankScraperFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ScrapeCaixaImobiliarioEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ScrapeSantanderEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScrapeCaixaImobiliarioEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ScrapeSantanderEnabled",
                table: "AppSettings");
        }
    }
}
