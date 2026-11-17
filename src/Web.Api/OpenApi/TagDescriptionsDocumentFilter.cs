using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Web.Api.Endpoints;

namespace Web.Api.OpenApi;

internal sealed class TagDescriptionsDocumentFilter : IDocumentFilter
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        [Tags.Users] = "Registration, authentication (JWT + rotating refresh tokens) and the current user's profile.",
        [Tags.Todos] = "To-do items of the current user (sample feature)."
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = new HashSet<OpenApiTag>(
            Descriptions.Select(tag => new OpenApiTag { Name = tag.Key, Description = tag.Value }));
    }
}
