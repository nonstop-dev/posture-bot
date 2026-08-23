using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonStop.Posture.Bot.Migrations
{
    /// <inheritdoc />
    public partial class Addhoursselect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartHour",
                table: "Subscribers",
                newName: "StartHourUtc");

            migrationBuilder.RenameColumn(
                name: "EndHour",
                table: "Subscribers",
                newName: "EndHourUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartHourUtc",
                table: "Subscribers",
                newName: "StartHour");

            migrationBuilder.RenameColumn(
                name: "EndHourUtc",
                table: "Subscribers",
                newName: "EndHour");
        }
    }
}
