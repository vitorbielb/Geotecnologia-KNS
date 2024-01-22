using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class dataArquivo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataCartografia",
                table: "Cartografias");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCartografia",
                table: "CartografiasArquivos",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataCartografia",
                table: "CartografiasArquivos");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCartografia",
                table: "Cartografias",
                type: "datetime2",
                nullable: true);
        }
    }
}
