using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZeMail.Infrastructure.Migrations
{
    public partial class AddFts5MessageSearch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIRTUAL TABLE IF NOT EXISTS MessageSearch
                USING fts5(
                    message_id UNINDEXED,
                    folder_id  UNINDEXED,
                    subject,
                    from_address,
                    from_name,
                    body_text,
                    sent_at_utc UNINDEXED,
                    content='',
                    tokenize='unicode61'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS MessageSearch;");
        }
    }
}