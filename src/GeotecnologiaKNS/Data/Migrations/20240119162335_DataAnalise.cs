using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class DataAnalise : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataAnalise",
                table: "AnalisesArquivos",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataAnalise",
                table: "AnalisesArquivos");
        }
    }
}
