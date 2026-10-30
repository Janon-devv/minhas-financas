using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhasFinancas.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class ChavesEstrangeiras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orfaos que a chave nova recusaria. O app nunca os cria (exclusao de lancamento leva
            // as situacoes, de carteira leva os diarios e recusa se tiver lancamento), entao isto
            // so pega resto de restauracao antiga feita antes da conferencia completa do arquivo.
            //  - Situacao sem lancamento: lixo perigoso, um Id reaproveitado herdaria o pagamento.
            //  - Diario de carteira que nao existe: nenhuma projecao olha para ele.
            //  - Lancamento de carteira que nao existe: volta para a conta corrente. Hoje ele some
            //    de toda projecao; aparecendo, o dono ve e corrige, que e melhor que sumir calado.
            // Fatura de cartao apagado fica como esta: apagar tiraria uma divida da previsao.
            migrationBuilder.Sql(
                """
                DELETE FROM "Situacoes" WHERE "LancamentoId" NOT IN (SELECT "Id" FROM "Lancamentos");
                DELETE FROM "DiariosReais" WHERE "CarteiraId" IS NOT NULL AND "CarteiraId" NOT IN (SELECT "Id" FROM "Carteiras");
                UPDATE "Lancamentos" SET "CarteiraId" = NULL WHERE "CarteiraId" IS NOT NULL AND "CarteiraId" NOT IN (SELECT "Id" FROM "Carteiras");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CarteiraId",
                table: "Lancamentos",
                column: "CarteiraId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiariosReais_Carteiras_CarteiraId",
                table: "DiariosReais",
                column: "CarteiraId",
                principalTable: "Carteiras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Faturas_Cartoes_CartaoId",
                table: "Faturas",
                column: "CartaoId",
                principalTable: "Cartoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_Carteiras_CarteiraId",
                table: "Lancamentos",
                column: "CarteiraId",
                principalTable: "Carteiras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Situacoes_Lancamentos_LancamentoId",
                table: "Situacoes",
                column: "LancamentoId",
                principalTable: "Lancamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiariosReais_Carteiras_CarteiraId",
                table: "DiariosReais");

            migrationBuilder.DropForeignKey(
                name: "FK_Faturas_Cartoes_CartaoId",
                table: "Faturas");

            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_Carteiras_CarteiraId",
                table: "Lancamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Situacoes_Lancamentos_LancamentoId",
                table: "Situacoes");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_CarteiraId",
                table: "Lancamentos");
        }
    }
}
