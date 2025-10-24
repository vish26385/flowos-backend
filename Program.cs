//using FlowOS.Api.Data;
//using FlowOS.Api.Models;
//using FlowOS.Api.Services;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.AspNetCore.HttpOverrides;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.OpenApi.Models;
//using System.Text;

//var builder = WebApplication.CreateBuilder(args);

//// ✅ 1. Database connection
//builder.Services.AddDbContext<FlowOSContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//// ✅ 2. Identity
//builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
//    .AddEntityFrameworkStores<FlowOSContext>()
//    .AddDefaultTokenProviders();

//// ✅ 3. JWT Authentication
//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
//})
//.AddJwtBearer(options =>
//{
//    options.TokenValidationParameters = new TokenValidationParameters
//    {
//        ValidateIssuer = false,
//        ValidateAudience = false,
//        ValidateLifetime = true,
//        ValidateIssuerSigningKey = true,
//        IssuerSigningKey = new SymmetricSecurityKey(
//            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
//    };
//});

//// ✅ 4. Controllers + Swagger
//builder.Services.AddScoped<TokenService>();
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FlowOS API", Version = "v1" });

//    // JWT setup in Swagger
//    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        Type = SecuritySchemeType.Http,
//        Scheme = "bearer",
//        BearerFormat = "JWT",
//        In = ParameterLocation.Header,
//        Description = "Enter your JWT token in the text box below.",
//    });

//    c.AddSecurityRequirement(new OpenApiSecurityRequirement
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
//            },
//            new string[]{}
//        }
//    });
//});

////// ✅ 5. Allow all CORS for testing
////builder.Services.AddCors(options =>
////{
////    options.AddPolicy("AllowAll", policy =>
////    {
////        policy.AllowAnyOrigin()
////              .AllowAnyHeader()
////              .AllowAnyMethod();
////    });
////});

//var app = builder.Build();

//// ✅ 6. Forward headers (for reverse proxies like Azure)
//app.UseForwardedHeaders(new ForwardedHeadersOptions
//{
//    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
//});

//// ✅ 7. Always enable Swagger (even in production)
//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlowOS API v1");
//    c.RoutePrefix = "swagger"; // keep swagger at /swagger
//});

//// ✅ 8. Root URL returns something (no more 404)
//app.MapGet("/", () => Results.Ok("✅ FlowOS API is running on Azure!"));

//// ✅ 9. Middleware order
//app.UseHttpsRedirection();
//app.UseRouting();
//app.UseCors("AllowAll");
//app.UseAuthentication();
//app.UseAuthorization();

//// ✅ 10. Map controllers
//app.MapControllers();

//app.Run();

using FlowOS.Api.Data;
using FlowOS.Api.Models;
using FlowOS.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ✅ Database
builder.Services.AddDbContext<FlowOSContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<FlowOSContext>()
    .AddDefaultTokenProviders();

// ✅ JWT
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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ClockSkew = TimeSpan.Zero
    };
});

// ✅ CORS (handles Expo tunnels + Azure)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy
            .AllowAnyOrigin()   // allow all origins (works fine since no credentials used)
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

// ✅ Swagger
builder.Services.AddScoped<TokenService>();
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
                Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[]{}
        }
    });
});

builder.Services.AddScoped<IEmailService, EmailService>();

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

var app = builder.Build();

// ✅ Middleware order (critical)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlowOS API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("AllowAll");       // ⚠️ Must come *after* UseRouting and *before* Auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok("✅ FlowOS API running on Azure"));

app.Run();

