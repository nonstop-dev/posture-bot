using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonStop.SitUpStraight.Bot.Migrations
{
    /// <inheritdoc />
    public partial class Addtimezoneoffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartHourUtc",
                table: "Subscribers",
                newName: "StartHour");

            migrationBuilder.RenameColumn(
                name: "EndHourUtc",
                table: "Subscribers",
                newName: "Offset");

            migrationBuilder.AddColumn<int>(
                name: "EndHour",
                table: "Subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndHour",
                table: "Subscribers");

            migrationBuilder.RenameColumn(
                name: "StartHour",
                table: "Subscribers",
                newName: "StartHourUtc");

            migrationBuilder.RenameColumn(
                name: "Offset",
                table: "Subscribers",
                newName: "EndHourUtc");
        }
    }
}
