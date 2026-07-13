using BachataFeedback.Api.Authorization;
using BachataFeedback.Api.Data;
using BachataFeedback.Api.Middleware;
using BachataFeedback.Api.Services;
using BachataFeedback.Api.Services.Antivirus;
using BachataFeedback.Api.Services.Images;
using BachataFeedback.Api.Services.Moderation;
using BachataFeedback.Api.Services.Storage;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// В design-time используем заглушку, чтобы не падать на недоступной БД
if (IsDesignTime())
{
    // Регистрируем DbContext с пустой строкой - он не будет использоваться
    // EF Tools возьмут контекст из IDesignTimeDbContextFactory
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql("Server=localhost;Database=dummy;",
            new MySqlServerVersion(new Version(8, 0, 36))));
}
else
{
    // Обычная регистрация для runtime
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)),
            mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null)));
}

// Функция определения design-time режима
static bool IsDesignTime()
{
    return Environment.GetCommandLineArgs().Any(arg =>
        arg.Contains("migrations") ||
        arg.Contains("database") ||
        arg.Contains("ef"));
}

// Identity
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 0;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Authorization policies (permission-based) + AdminOnly role policy
builder.Services.AddAuthorization(options =>
{
    foreach (var perm in Permissions.All.Distinct())
    {
        options.AddPolicy(perm, policy => policy.RequireClaim("permission", perm));
    }

    options.AddPolicy("AdminOnly", policy => policy.RequireRole(SystemRoles.Admin));
});

var allowedOrigins = new[]
{
    "https://bachata.alexei.site",
    "https://api-bachata.alexei.site",
    "http://localhost:3000",
    "http://localhost:5000"
};

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Единый обработчик отклонённых запросов (проставляем CORS и JSON)
    options.OnRejected = async (context, token) =>
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Проставим CORS-заголовки вручную, чтобы браузер не ругался
        var origin = http.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin) &&
            allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            http.Response.Headers["Access-Control-Allow-Origin"] = origin;
            http.Response.Headers["Vary"] = "Origin";
            http.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            // Чтобы фронт мог прочитать заголовок (если решите добавить), можно экспонировать:
            http.Response.Headers["Access-Control-Expose-Headers"] = "Retry-After";
        }

        http.Response.ContentType = "application/json";
        await http.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { success = false, message = "Too many requests" }),
            token);
    };

    // Политика для /auth — исключаем OPTIONS, лимитируем по IP
    options.AddPolicy("auth", http =>
    {
        if (HttpMethods.IsOptions(http.Request.Method))
            return RateLimitPartition.GetNoLimiter("preflight-auth");

        var key = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // Политика для /reports — аналогично, исключаем OPTIONS
    options.AddPolicy("reports", http =>
    {
        if (HttpMethods.IsOptions(http.Request.Method))
            return RateLimitPartition.GetNoLimiter("preflight-reports");

        var key = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services.AddTransient<IClaimsTransformation, RolePermissionsClaimsTransformation>();

// Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// Media pipeline
builder.Services.AddSingleton<IStorageService, MinioStorageService>();
builder.Services.AddSingleton<IAntivirusScanner, ClamAvScanner>();
builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();

builder.Services.AddSingleton<IModerationQueue, RabbitMqModerationQueue>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Unified model validation error
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .ToArray();

        var message = errors.Length > 0 ? string.Join("; ", errors) : "Invalid request";
        return new BadRequestObjectResult(new { success = false, message });
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Bachata Feedback API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS: whitelist
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Migrate
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Seed Roles & Permissions
await IdentitySeeder.SeedAsync(app.Services);

// Exception middleware
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();