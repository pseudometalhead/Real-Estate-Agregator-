using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EstateAggregator.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DistrictsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PriceMin = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PriceMax = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RoomsMin = table.Column<int>(type: "INTEGER", nullable: false),
                    RoomsMax = table.Column<int>(type: "INTEGER", nullable: false),
                    ScrapeIdealistaEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScrapeImoVirtualEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScrapeImobiliarioEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LogFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    LastScrapedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LocationString = table.Column<string>(type: "TEXT", nullable: true),
                    Beds = table.Column<int>(type: "INTEGER", nullable: true),
                    Baths = table.Column<int>(type: "INTEGER", nullable: true),
                    SizeM2 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SunOrientation = table.Column<string>(type: "TEXT", nullable: false),
                    OrientationSource = table.Column<string>(type: "TEXT", nullable: false),
                    PhotosJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourcePropertyId = table.Column<string>(type: "TEXT", nullable: true),
                    DedupHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FirstScrapedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScraperRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    RunStartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RunEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PropertiesFound = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertiesAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertiesUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertiesSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    HasErrors = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScraperRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyListings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AgentName = table.Column<string>(type: "TEXT", nullable: true),
                    AgentPhone = table.Column<string>(type: "TEXT", nullable: true),
                    AgentEmail = table.Column<string>(type: "TEXT", nullable: true),
                    AskedAboutOrientation = table.Column<bool>(type: "INTEGER", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyListings_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MyListings_PropertyId",
                table: "MyListings",
                column: "PropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MyListings_Status",
                table: "MyListings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Beds",
                table: "Properties",
                column: "Beds");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_DedupHash",
                table: "Properties",
                column: "DedupHash");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_LastSeenAt",
                table: "Properties",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_LocationString",
                table: "Properties",
                column: "LocationString");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Price",
                table: "Properties",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Source",
                table: "Properties",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_Url",
                table: "Properties",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScraperRuns_RunStartTime",
                table: "ScraperRuns",
                column: "RunStartTime");

            migrationBuilder.CreateIndex(
                name: "IX_ScraperRuns_Source",
                table: "ScraperRuns",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "MyListings");

            migrationBuilder.DropTable(
                name: "ScraperRuns");

            migrationBuilder.DropTable(
                name: "Properties");
        }
    }
}
