using FlowOS.Api.Configurations;
using FlowOS.Api.Data;
using FlowOS.Api.Models;
using FlowOS.Api.Services;
using FlowOS.Api.Services.Notifications;
using FlowOS.Api.Services.Planner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenAI; // ✅ Official SDK
using System.ClientModel;
using System.Text;
// using OpenAI.Chat; // (If you reference typed Chat client elsewhere)

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ✅ Configure logging level globally
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ---------------------------
// 1) Database
// ---------------------------
builder.Services.AddDbContext<FlowOSContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------------------
// 2) Identity
// ---------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddEntityFrameworkStores<FlowOSContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});

// ---------------------------
// 3) JWT Auth
// ---------------------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.Zero
    };
});

// ---------------------------
// 4) CORS (Expo / Web / Mobile)
// ---------------------------
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll", policy =>
//    {
//        policy
//            .AllowAnyOrigin()   // Safe because you're not using cookies/credentials
//            .AllowAnyHeader()
//            .AllowAnyMethod();
//    });
//});

// ---------------------------
// 4) CORS (Expo / Web / Mobile)
// ---------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        x => x
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true));   // TEMPORARY FOR DEV
});

// ---------------------------
// 5) Controllers + Swagger
// ---------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FlowOS API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
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

// ---------------------------
// 6) App Services
// ---------------------------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Orchestrator visible to the app:
builder.Services.AddScoped<IPlannerService, PlannerService>();

// Concrete AI engine (partial class files) — always add
// (PlannerService will call it; config may decide behavior inside)
builder.Services.AddScoped<OpenAIPlannerService>();

// HttpClient for any REST the AI service might perform
builder.Services.AddHttpClient<OpenAIPlannerService>();

// ✅ Bind ExpoPush from appsettings.json using ONE options class (Configurations)
builder.Services.Configure<ExpoPushOptions>(
    builder.Configuration.GetSection("ExpoPush")
);

// ✅ Expo client + worker
builder.Services.AddHttpClient<ExpoPushClient>();
builder.Services.AddHostedService<NudgeWorker>();

//// ---------------------------
//// 7) Settings Binding
//// ---------------------------
//builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
//builder.Services.AddSingleton(sp =>
//{
//    var config = sp.GetRequiredService<IConfiguration>();
//    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
//                 ?? config["OpenAI:ApiKey"];

//    if (string.IsNullOrWhiteSpace(apiKey))
//        throw new InvalidOperationException("Missing OpenAI API key.");

//    return new OpenAIClient(apiKey);
//});

//var useOpenAI = builder.Configuration.GetValue<bool>("Planner:UseOpenAI");

//// ---------------------------
//// 8) OpenAI SDK Client (OC1)
//// ---------------------------
//// We register the official OpenAIClient as a singleton.
//// It uses OpenAISettings.ApiKey (and optional Organization).
//builder.Services.AddSingleton(sp =>
//{
//    var settings = sp.GetRequiredService<IOptions<OpenAISettings>>().Value;

//    if (string.IsNullOrWhiteSpace(settings.ApiKey))
//    {
//        if (useOpenAI)
//            throw new InvalidOperationException("❌ OpenAI:ApiKey is missing in configuration.");

//        // If AI disabled, return a dummy client
//        // (We avoid null so DI still works, but this client should not be used)
//        return new OpenAIClient(new ApiKeyCredential("DUMMY_KEY"));
//    }

//    var opts = new OpenAIClientOptions();
//    return new OpenAIClient(new ApiKeyCredential(settings.ApiKey!), opts);
//});

builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // Prefer environment variable in production (Render)
    var apiKey =
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? config["OpenAI:ApiKey"];

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Missing OpenAI API key. Set OPENAI_API_KEY env var or OpenAI:ApiKey config.");

    var opts = new OpenAIClientOptions();
    return new OpenAIClient(new ApiKeyCredential(apiKey), opts);
});

// You may also want IHttpClientFactory generally:
builder.Services.AddHttpClient();

// ---------------------------
// 9) App Pipeline
// ---------------------------
var app = builder.Build();

var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
app.Logger.LogInformation("OPENAI_API_KEY present: {Present}, length: {Len}",
    !string.IsNullOrWhiteSpace(envKey),
    string.IsNullOrWhiteSpace(envKey) ? 0 : envKey.Length);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

static async System.Threading.Tasks.Task SeedRolesAndAdminUserAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // ---- Ensure Roles Exist ----
    var roles = new[] { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // ---- Create Default Admin ----
    var adminEmail = "kgvishnupandit@gmail.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser is null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "FlowOS Admin"
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@123");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    return; // 👈 Fix CS0161
}

// Trust X-Forwarded-* (Azure/AppGW/NGINX)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlowOS API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowAll");   // between UseRouting and Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Simple health root
app.MapGet("/", () => Results.Ok("✅ FlowOS API running"));

// ⬇️ ADD THIS BEFORE app.Run()
await SeedRolesAndAdminUserAsync(app);

// ---------------------------
// 10) Run
// ---------------------------
app.Run();
