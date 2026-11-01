using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EcoStream.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "geo");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "neighborhoods",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    population = table.Column<int>(type: "integer", nullable: true),
                    boundary = table.Column<MultiPolygon>(type: "geography(MultiPolygon,4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_neighborhoods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "risk_zones",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    risk_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    boundary = table.Column<MultiPolygon>(type: "geography(MultiPolygon,4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_zones", x => x.id);
                    table.CheckConstraint("ck_risk_zones_level", "level BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "rivers",
                schema: "geo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    course = table.Column<MultiLineString>(type: "geography(MultiLineString,4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stations", x => x.id);
                    table.UniqueConstraint("ak_stations_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location = table.Column<Point>(type: "geography(Point,4326)", nullable: false),
                    measured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rainfall_mm_per_hour = table.Column<double>(type: "double precision", nullable: false),
                    wind_gust_kmh = table.Column<double>(type: "double precision", nullable: false),
                    pressure_hpa = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_telemetry_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_telemetry_readings_stations_station_code",
                        column: x => x.station_code,
                        principalTable: "stations",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_neighborhoods_boundary",
                schema: "geo",
                table: "neighborhoods",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_neighborhoods_city_name",
                schema: "geo",
                table: "neighborhoods",
                columns: new[] { "city", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_zones_boundary",
                schema: "geo",
                table: "risk_zones",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_risk_zones_name",
                schema: "geo",
                table: "risk_zones",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rivers_course",
                schema: "geo",
                table: "rivers",
                column: "course")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_rivers_name",
                schema: "geo",
                table: "rivers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stations_location",
                table: "stations",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_readings_location",
                table: "telemetry_readings",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_telemetry_readings_station_code_measured_at",
                table: "telemetry_readings",
                columns: new[] { "station_code", "measured_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "neighborhoods",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "risk_zones",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "rivers",
                schema: "geo");

            migrationBuilder.DropTable(
                name: "telemetry_readings");

            migrationBuilder.DropTable(
                name: "stations");
        }
    }
}
