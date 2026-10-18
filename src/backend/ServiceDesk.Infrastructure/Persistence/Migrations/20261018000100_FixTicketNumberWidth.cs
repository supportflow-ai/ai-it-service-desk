using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ServiceDesk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261018000100_FixTicketNumberWidth")]
public sealed class FixTicketNumberWidth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION public.next_ticket_number() RETURNS text
        LANGUAGE plpgsql VOLATILE AS $$
        DECLARE sequence_text text;
        BEGIN
            sequence_text := nextval('public.ticket_number_seq')::text;
            RETURN 'IT-' || to_char(now(), 'YYYY') || '-' ||
                lpad(sequence_text, greatest(4, length(sequence_text)), '0');
        END;
        $$;
        ALTER TABLE public.tickets ALTER COLUMN ticket_number SET DEFAULT public.next_ticket_number();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE public.tickets ALTER COLUMN ticket_number SET DEFAULT
            'IT-' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(nextval('ticket_number_seq')::TEXT, 4, '0');
        DROP FUNCTION public.next_ticket_number();
        """);
}
