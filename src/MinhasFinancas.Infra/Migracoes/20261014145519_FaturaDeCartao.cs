using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhasFinancas.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class FaturaDeCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PapelNaFatura",
                table: "Lancamentos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "JurosRotativoMensal",
                table: "Cartoes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Faturas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CartaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Fechamento = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalInformadoCentavos = table.Column<long>(type: "INTEGER", nullable: true),
                    PagoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    PagoEm = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Obs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faturas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faturas_CartaoId_Vencimento",
                table: "Faturas",
                columns: new[] { "CartaoId", "Vencimento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Faturas");

            migrationBuilder.DropColumn(
                name: "PapelNaFatura",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "JurosRotativoMensal",
                table: "Cartoes");
        }
    }
}
