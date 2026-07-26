using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using ExpenseManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Expense Manager API",
        Version = "v1"
    });
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt__Secret is required.");
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Jwt__Secret must contain at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExpenseManager",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ExpenseManager.App",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => context.HttpContext.RequestServices
                .GetRequiredService<IJwtTokenVersionValidator>()
                .ValidateAsync(context)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimitPolicies.General, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy(AuthRateLimitPolicies.PasswordResetRequest, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy(AuthRateLimitPolicies.PasswordResetVerify, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IJwtTokenVersionValidator, JwtTokenVersionValidator>();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddSingleton<IReceiptImageReader, ReceiptImageReader>();
builder.Services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
builder.Services.Configure<ReceiptProcessingOptions>(
    builder.Configuration.GetSection("ReceiptProcessing"));
builder.Services.AddHostedService<ReceiptProcessingWorker>();
builder.Services.AddScoped<IReceiptConfirmationService, ReceiptConfirmationService>();
builder.Services.AddScoped<ICategorySuggestionService, CategorySuggestionService>();
builder.Services.AddSingleton<IExcelReportService, ExcelReportService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISecurityTokenGenerator, SecurityTokenGenerator>();
builder.Services.AddSingleton<IAuthSecretHasher, HmacAuthSecretHasher>();
builder.Services.AddSingleton<IAccountCodeSender, SmtpAccountCodeSender>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();
builder.Services.AddHttpClient<IOcrClient, OcrClient>(client =>
{
    var baseUrl = builder.Configuration["OCR_SERVICE_BASE_URL"] ?? "http://ocr-service:8000";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ocr:TimeoutSeconds", 120));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigrations");
    await ReceiptImageMigration.ApplyAsync(
        db,
        logger,
        app.Configuration["LegacyReceiptPath"]);
}

app.Run();

public partial class Program;
