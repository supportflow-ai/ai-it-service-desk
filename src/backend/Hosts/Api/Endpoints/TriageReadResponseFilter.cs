using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Metadata;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ServiceDesk.Api.Endpoints;

/// <summary>Cùng mã 200 có hai hình chiếu theo quyền; không để metadata cuối ghi đè hình chiếu trước.</summary>
public sealed class TriageReadResponseFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId != "GetTicketAiTriage") return;
        operation.Responses["200"].Content["application/json"].Schema = new OpenApiSchema
        {
            AnyOf = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IProducesResponseTypeMetadata>()
                .Where(metadata => metadata.StatusCode == 200 && metadata.Type is not null && metadata.Type != typeof(void))
                .Select(metadata => metadata.Type!).Distinct()
                .Select(type => context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository)).ToList()
        };
    }
}
