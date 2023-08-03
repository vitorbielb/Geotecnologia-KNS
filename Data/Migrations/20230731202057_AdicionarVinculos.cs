using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarVinculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vinculos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identificacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Propriedade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CadastroRural = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsavel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProdutorId = table.Column<int>(type: "int", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vinculos");
        }
    }
}
