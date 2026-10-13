using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinhasFinancas.Infra.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ancoras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SaldoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ancoras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cartoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    DiaFechamento = table.Column<int>(type: "INTEGER", nullable: false),
                    DiaVencimento = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SaldoAncoraCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    DataAncora = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DiarioPrevistoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    SaldoConfortoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    ContarEntradasIncertas = table.Column<bool>(type: "INTEGER", nullable: false),
                    JaGuardadoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    HorizonteMeses = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfiguracaoConcluida = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiariosReais",
                columns: table => new
                {
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValorCentavos = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiariosReais", x => x.Data);
                });

            migrationBuilder.CreateTable(
                name: "Metas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    ObjetivoCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    Prazo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SaldoInicialCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    ArquivadaEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lancamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ValorCentavos = table.Column<long>(type: "INTEGER", nullable: false),
                    Vezes = table.Column<int>(type: "INTEGER", nullable: true),
                    CartaoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Certeza = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TagId = table.Column<int>(type: "INTEGER", nullable: true),
                    MetaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Obs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lancamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "Cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Metas_MetaId",
                        column: x => x.MetaId,
                        principalTable: "Metas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cartoes_Nome",
                table: "Cartoes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CartaoId",
                table: "Lancamentos",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_Data",
                table: "Lancamentos",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_MetaId",
                table: "Lancamentos",
                column: "MetaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_TagId",
                table: "Lancamentos",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Metas_Nome",
                table: "Metas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Nome",
                table: "Tags",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ancoras");

            migrationBuilder.DropTable(
                name: "Configuracoes");

            migrationBuilder.DropTable(
                name: "DiariosReais");

            migrationBuilder.DropTable(
                name: "Lancamentos");

            migrationBuilder.DropTable(
                name: "Cartoes");

            migrationBuilder.DropTable(
                name: "Metas");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
