using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    /// <inheritdoc />
    public partial class FilaDeAnalises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProximaTentativaEm",
                table: "AnalisesAutomaticas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tentativas",
                table: "AnalisesAutomaticas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AnalisesAutomaticas_Situacao_ProximaTentativaEm",
                table: "AnalisesAutomaticas",
                columns: new[] { "Situacao", "ProximaTentativaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalisesAutomaticas_Situacao_ProximaTentativaEm",
                table: "AnalisesAutomaticas");

            migrationBuilder.DropColumn(
                name: "ProximaTentativaEm",
                table: "AnalisesAutomaticas");

            migrationBuilder.DropColumn(
                name: "Tentativas",
                table: "AnalisesAutomaticas");
        }
    }
}
