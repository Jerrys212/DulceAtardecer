using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>())
    .UseSeeding((context, _) => DataSeeder.Seed(context, builder.Configuration))
    .UseAsyncSeeding((context, _, cancellationToken) =>
        DataSeeder.SeedAsync(context, builder.Configuration, cancellationToken)));

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
builder.Services.AddHostedService<ExpiredSessionCleanupService>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IExtraRepository, ExtraRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();
builder.Services.AddHealthChecks();
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
                    context.Fail(AuthFailures.SessionRevoked);
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();

                string reason = context.AuthenticateFailure switch
                {
                    null => "No se envió un token de acceso",
                    SecurityTokenExpiredException => "El token de acceso expiró",
                    { Message: AuthFailures.SessionRevoked } => "La sesión fue cerrada o revocada",
                    _ => "El token de acceso no es válido",
                };

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(StatusCodes.Status401Unauthorized, errors: [reason]));
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRouting(options => options.LowercaseUrls = true);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<MustChangePasswordFilter>();
        options.Filters.Add<ApiResponseStatusCodeFilter>();
        options.Filters.Add(new ProducesAttribute("application/json"));
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict)
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = context =>
        {
            List<string> jsonErrors = context.ModelState.Keys
                .Where(key => key.StartsWith('$'))
                .Select(key => key == "$"
                    ? "El cuerpo de la solicitud no es un JSON válido"
                    : $"El campo '{key.TrimStart('$', '.')}' tiene un tipo o formato inválido")
                .ToList();

            if (jsonErrors.Count > 0)
            {
                return new BadRequestObjectResult(ApiResponse.Fail(
                    StatusCodes.Status400BadRequest,
                    "El cuerpo de la solicitud no tiene un formato JSON válido",
                    jsonErrors));
            }

            List<string> errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value?.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                    ? $"El campo '{JsonNamingPolicy.CamelCase.ConvertName(entry.Key)}' tiene un valor inválido"
                    : error.ErrorMessage) ?? [])
                .ToList();

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

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<ApiResponseStatusExampleTransformer>();
});

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PolicyNames.AllowSpecificOrigin, policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT")
    .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "Idempotency-Key")
    .AllowCredentials()));

MapsterConfig.RegisterMappings();

var app = builder.Build();

if (args.FirstOrDefault() == ResetAdminCommand.Name)
{
    Environment.ExitCode = await ResetAdminCommand.RunAsync(app.Services, args);
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async context =>
{
    HttpResponse response = context.HttpContext.Response;
    await response.WriteAsJsonAsync(ApiResponse.Fail(response.StatusCode));
});

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
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
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
