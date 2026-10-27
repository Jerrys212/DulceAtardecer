using System.Text.Json;
using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Data;
using DulceAtardecer.Mapping;
using DulceAtardecer.Middleware;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.ModelState.Keys.Any(key => key.StartsWith('$')))
            {
                return new BadRequestObjectResult(ApiResponse.Fail(
                    StatusCodes.Status400BadRequest,
                    "El cuerpo de la solicitud no tiene un formato JSON válido"));
            }

            Dictionary<string, string[]> errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                    entry => entry.Value?.Errors
                        .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "Valor inválido" : error.ErrorMessage)
                        .ToArray() ?? []);

            return new BadRequestObjectResult(
                ApiResponse.Fail(StatusCodes.Status400BadRequest, "Hay errores de validación", errors));
        };
    });

builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddOpenApi("v1");

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PolicyNames.AllowSpecificOrigin, policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT")
    .WithHeaders("Authorization", "Content-Type", "X-Requested-With")
    .AllowCredentials()));

MapsterConfig.RegisterMappings();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async context =>
{
    HttpResponse response = context.HttpContext.Response;
    await response.WriteAsJsonAsync(ApiResponse.Fail(response.StatusCode));
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/swagger", () => Results.Content(SwaggerUiPage.Html, "text/html")).ExcludeFromDescription();
}

app.UseHttpsRedirection();
app.UseCors(PolicyNames.AllowSpecificOrigin);

app.UseAuthorization();

app.MapControllers();

app.Run();
