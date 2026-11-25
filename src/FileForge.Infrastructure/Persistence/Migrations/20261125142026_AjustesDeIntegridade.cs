using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AjustesDeIntegridade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contas_cliente_id_agencia_numero",
                table: "contas");

            migrationBuilder.CreateIndex(
                name: "ix_contas_cliente_id",
                table: "contas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ux_contas_agencia_numero",
                table: "contas",
                columns: new[] { "agencia", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_clientes_documento",
                table: "clientes",
                column: "documento",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contas_cliente_id",
                table: "contas");

            migrationBuilder.DropIndex(
                name: "ux_contas_agencia_numero",
                table: "contas");

            migrationBuilder.DropIndex(
                name: "ux_clientes_documento",
                table: "clientes");

            migrationBuilder.CreateIndex(
                name: "ix_contas_cliente_id_agencia_numero",
                table: "contas",
                columns: new[] { "cliente_id", "agencia", "numero" },
                unique: true);
        }
    }
}
