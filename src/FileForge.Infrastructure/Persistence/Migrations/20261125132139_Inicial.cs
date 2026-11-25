using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "solicitacoes_arquivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_referencia = table.Column<DateOnly>(type: "date", nullable: false),
                    solicitado_por = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    nome_arquivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    motivo_falha = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tentativas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_arquivo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descricao = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transacoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agencia = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    numero = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas", x => x.id);
                    table.ForeignKey(
                        name: "fk_contas_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contas_cliente_id_agencia_numero",
                table: "contas",
                columns: new[] { "cliente_id", "agencia", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_solicitacao_ativa",
                table: "solicitacoes_arquivo",
                columns: new[] { "cliente_id", "data_referencia" },
                unique: true,
                filter: "status IN ('Pendente', 'Processando')");

            migrationBuilder.CreateIndex(
                name: "ix_transacoes_conta_id_data",
                table: "transacoes",
                columns: new[] { "conta_id", "data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas");

            migrationBuilder.DropTable(
                name: "solicitacoes_arquivo");

            migrationBuilder.DropTable(
                name: "transacoes");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
