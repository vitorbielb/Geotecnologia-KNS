using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeotecnologiaKNS.Data.Migrations
{
    public partial class RemoveDataCart : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataCartografia",
                table: "CartografiasArquivos");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataCartografia",
                table: "CartografiasArquivos",
                type: "datetime2",
                nullable: true);
        }
    }
}
