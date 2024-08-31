using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NonStop.SitUpStraight.Bot.Migrations
{
    /// <inheritdoc />
    public partial class Adddaysselect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysPerWeek",
                table: "Subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DaysPerWeek",
                table: "Subscribers");
        }
    }
}
