using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechStore.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMarca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "Producto",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Marca",
                table: "Producto");
        }
    }
}
