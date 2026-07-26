using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstateAggregator.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyGeoCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Lat",
                table: "Properties",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Lng",
                table: "Properties",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Lng",
                table: "Properties");
        }
    }
}
