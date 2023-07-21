using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Atualizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Propriedade",
                table: "Propriedade");

            migrationBuilder.RenameTable(
                name: "Propriedade",
                newName: "Propriedades");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Propriedades",
                table: "Propriedades",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Propriedades",
                table: "Propriedades");

            migrationBuilder.RenameTable(
                name: "Propriedades",
                newName: "Propriedade");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Propriedade",
                table: "Propriedade",
                column: "Id");
        }
    }
}
