using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhasFinancas.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class PagamentosDeFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ordem trocada a mao: a tabela nova nasce e recebe o que estava pago ANTES de as
            // colunas antigas sumirem. Na ordem gerada pelo EF, o pago de toda fatura se perdia.
            migrationBuilder.CreateTable(
                name: "PagamentosFatura",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FaturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValorCentavos = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosFatura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosFatura_Faturas_FaturaId",
                        column: x => x.FaturaId,
                        principalTable: "Faturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosFatura_FaturaId",
                table: "PagamentosFatura",
                column: "FaturaId");

            // O formato antigo guardava o pago somado e a data do ultimo pagamento. Quando saiu
            // cada parte nunca foi gravado, entao vira um pagamento so, na data do ultimo (ou no
            // vencimento, se nao havia data). Pago zero ou negativo nao vira pagamento.
            migrationBuilder.Sql(
                """
                INSERT INTO "PagamentosFatura" ("FaturaId", "Data", "ValorCentavos")
                SELECT "Id", COALESCE("PagoEm", "Vencimento"), "PagoCentavos"
                FROM "Faturas"
                WHERE "PagoCentavos" > 0;
                """);

            migrationBuilder.DropColumn(
                name: "PagoCentavos",
                table: "Faturas");

            migrationBuilder.DropColumn(
                name: "PagoEm",
                table: "Faturas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PagoCentavos",
                table: "Faturas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PagoEm",
                table: "Faturas",
                type: "TEXT",
                nullable: true);

            // Volta ao formato antigo somando os pagamentos, com a data do ultimo.
            migrationBuilder.Sql(
                """
                UPDATE "Faturas" SET
                    "PagoCentavos" = COALESCE((SELECT SUM(p."ValorCentavos") FROM "PagamentosFatura" p WHERE p."FaturaId" = "Faturas"."Id"), 0),
                    "PagoEm" = (SELECT MAX(p."Data") FROM "PagamentosFatura" p WHERE p."FaturaId" = "Faturas"."Id");
                """);

            migrationBuilder.DropTable(
                name: "PagamentosFatura");
        }
    }
}
