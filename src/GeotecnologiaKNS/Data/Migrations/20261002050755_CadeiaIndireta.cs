using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CadeiaIndireta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CadeiaIndireta",
                table: "Regras",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FornecedoresIndiretos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PropriedadeId = table.Column<int>(type: "int", nullable: false),
                    CodigoCar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomeProdutor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Documento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeclaradoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FornecedoresIndiretos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FornecedoresIndiretos_Industrias_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Industrias",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FornecedoresIndiretos_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FornecedoresIndiretos_PropriedadeId",
                table: "FornecedoresIndiretos",
                column: "PropriedadeId");

            migrationBuilder.CreateIndex(
                name: "IX_FornecedoresIndiretos_TenantId_PropriedadeId",
                table: "FornecedoresIndiretos",
                columns: new[] { "TenantId", "PropriedadeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FornecedoresIndiretos");

            migrationBuilder.DropColumn(
                name: "CadeiaIndireta",
                table: "Regras");
        }
    }
}
