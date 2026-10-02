using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookExchange.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListingCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Listings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Fiction"); // listings created before categories existed; owners can change it

            migrationBuilder.AddColumn<bool>(
                name: "GoodForBeginners",
                table: "Listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "GoodForBeginners",
                table: "Listings");
        }
    }
}
