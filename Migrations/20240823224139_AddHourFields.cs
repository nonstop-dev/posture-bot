using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonStop.SitUpStraight.Bot.Migrations
{
    /// <inheritdoc />
    public partial class AddHourFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndHourUtc",
                table: "Subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartHourUtc",
                table: "Subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndHourUtc",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "StartHourUtc",
                table: "Subscribers");
        }
    }
}
