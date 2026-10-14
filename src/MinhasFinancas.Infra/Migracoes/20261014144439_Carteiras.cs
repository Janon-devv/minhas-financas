using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhasFinancas.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class Carteiras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DiariosReais",
                table: "DiariosReais");

            migrationBuilder.AddColumn<int>(
                name: "CarteiraId",
                table: "Lancamentos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "DiariosReais",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "CarteiraId",
                table: "DiariosReais",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiariosReais",
                table: "DiariosReais",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Carteiras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    SaldoAncoraCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    DataAncora = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DiarioPrevistoCentavos = table.Column<long>(type: "INTEGER", nullable: true),
                    SaldoConfortoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carteiras", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiariosReais_CarteiraId_Data",
                table: "DiariosReais",
                columns: new[] { "CarteiraId", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carteiras_Nome",
                table: "Carteiras",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Carteiras");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DiariosReais",
                table: "DiariosReais");

            migrationBuilder.DropIndex(
                name: "IX_DiariosReais_CarteiraId_Data",
                table: "DiariosReais");

            migrationBuilder.DropColumn(
                name: "CarteiraId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DiariosReais");

            migrationBuilder.DropColumn(
                name: "CarteiraId",
                table: "DiariosReais");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DiariosReais",
                table: "DiariosReais",
                column: "Data");
        }
    }
}
