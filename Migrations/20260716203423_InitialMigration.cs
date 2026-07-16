using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ITMOParser.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "itmo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AbiturientId = table.Column<int>(type: "integer", nullable: false),
                    PointsFinal = table.Column<int>(type: "integer", nullable: false),
                    PointsBase = table.Column<int>(type: "integer", nullable: false),
                    Exams = table.Column<List<int>>(type: "integer[]", nullable: false),
                    IndividualAchievements = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Advantage = table.Column<bool>(type: "boolean", nullable: false),
                    Confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    Profile = table.Column<string>(type: "text", nullable: false),
                    Passes = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itmo", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itmo");
        }
    }
}
