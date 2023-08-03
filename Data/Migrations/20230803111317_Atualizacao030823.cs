using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Atualizacao030823 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vinculos");

            migrationBuilder.DropColumn(
                name: "Ccir",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Incra",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "LicencaAmbiental",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Matricula",
                table: "Propriedades");

            migrationBuilder.DropColumn(
                name: "Outros",
                table: "Propriedades");

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Produtores",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(18)",
                oldMaxLength: 18);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ccir",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "Propriedades",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Incra",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicencaAmbiental",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Matricula",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outros",
                table: "Propriedades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "Produtores",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14);

            migrationBuilder.CreateTable(
                name: "Vinculos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CadastroRural = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identificacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProdutorId = table.Column<int>(type: "int", nullable: true),
                    Propriedade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsavel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vinculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vinculos_Produtores_ProdutorId",
                        column: x => x.ProdutorId,
                        principalTable: "Produtores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vinculos_ProdutorId",
                table: "Vinculos",
                column: "ProdutorId");
        }
    }
}
