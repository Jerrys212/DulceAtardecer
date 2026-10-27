using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Data;
using DulceAtardecer.Mapping;
using DulceAtardecer.Middleware;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Settings;
using DulceAtardecer.Repository;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AuditableEntityInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) => options
    .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

builder.Services.AddOptions<ApiSettings>().BindConfiguration(ApiSettings.SectionName);
builder.Services.AddOptions<SessionSettings>().BindConfiguration(SessionSettings.SectionName);

ApiSettings apiSettings = builder.Configuration.GetSection(ApiSettings.SectionName).Get<ApiSettings>() ?? new ApiSettings();
if (Encoding.UTF8.GetByteCount(apiSettings.SecretKey) < ApiSettings.MinimumKeyBytes)
{
    throw new InvalidOperationException(
        $"ApiSettings:SecretKey must be at least {ApiSettings.MinimumKeyBytes} bytes.");
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>()
    .AddPasswordValidator<UsernameAsPasswordValidator>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IRevokedSessionCache, RevokedSessionCache>();
builder.Services.AddHostedService<RevokedSessionCacheLoader>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PolicyNames.LoginRateLimit, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = apiSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = apiSettings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = apiSettings.CreateSigningKey(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaimTypes.Name,
            RoleClaimType = AppClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                IRevokedSessionCache revokedSessions =
                    context.HttpContext.RequestServices.GetRequiredService<IRevokedSessionCache>();
                string? sessionId = context.Principal?.FindFirstValue(AppClaimTypes.SessionId);

                if (!Guid.TryParse(sessionId, out Guid parsedSessionId) || revokedSessions.IsRevoked(parsedSessionId))
                {
                    context.Fail("Session revoked");
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddControllers(options => options.Filters.Add<MustChangePasswordFilter>())
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

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
