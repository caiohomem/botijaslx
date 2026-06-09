using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Botijas.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadlineMessageTemplateToAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeadlineMessageTemplate",
                table: "AppSettings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "Olá {name}! As suas {count} botija(s) de CO₂ já estão prontas e à sua espera há {days} dia(s). Venha buscá-las quando puder!");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadlineMessageTemplate",
                table: "AppSettings");
        }
    }
}
