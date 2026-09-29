using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class SenhaProvisoria : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Falso para quem já existe, de propósito: essas senhas já são as
            // que os titulares escolheram ou vêm usando. A marca é para senhas
            // definidas por terceiros daqui em diante.
            migrationBuilder.AddColumn<bool>(
                name: "SenhaProvisoria",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenhaProvisoria",
                table: "AspNetUsers");
        }
    }
}
