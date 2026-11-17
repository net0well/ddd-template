using Microsoft.OpenApi;

namespace Web.Api.Extensions;

internal static class SwaggerExtensions
{
    private const string BearerScheme = "Bearer";

    internal static IServiceCollection AddSwaggerWithAuth(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Web.Api", Version = "v1" });

            // Several slices have a class with the same name (TodoResponse, Request…); ids must be unique.
            options.CustomSchemaIds(SchemaId);

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by POST /users/login."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = []
            });
        });

        return services;
    }

    internal static WebApplication UseSwaggerWithUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Web.Api v1");
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
        });

        return app;
    }

    // "CommandResultOfGuid", "GetByIdTodoResponse", "TodosCreateRequest"…
    private static string SchemaId(Type type)
    {
        string name = type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}Of{string.Concat(type.GetGenericArguments().Select(SchemaId))}"
            : type.Name;

        if (type.IsNested)
        {
            return SchemaId(type.DeclaringType!) + name;
        }

        string? ns = type.Namespace;

        bool isSharedType = ns is null ||
                            ns.StartsWith("System", StringComparison.Ordinal) ||
                            ns.StartsWith("SharedKernel", StringComparison.Ordinal) ||
                            ns.StartsWith("Domain", StringComparison.Ordinal);

        return isSharedType ? name : ns![(ns!.LastIndexOf('.') + 1)..] + name;
    }
}
