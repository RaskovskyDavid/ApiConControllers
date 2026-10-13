using ApiConControllers.Auth;
using ApiConControllers.Data;
using ApiConControllers.Entities;
using ApiConControllers.Middlewears;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
// 1. Read the "JwtSettings" section into a strongly-typed object.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
// 2. EF Core + ASP.NET Core Identity. InMemory keeps this sample runnable with zero database setup.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// AddIdentityCore (not AddIdentity) so Identity does NOT replace our JWT default auth scheme with cookies.
builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddIdentitySeeder(builder.Configuration);
// Named authorization policies (role-based and claim-based) - see Auth/Policies.cs.
builder.Services.AddAuthorizationPolicies();
// Add services to the container.

// 3. Tell ASP.NET Core to authenticate requests using JWT bearer tokens.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Keep the claim names exactly as they appear in the token (no surprise remapping).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            // No grace period on expiry - the token dies exactly when it says it does.
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Name,
            // RoleClaimType must match the claim name TokenService writes roles under.
            // If these two disagree, every role check fails with 403.
            RoleClaimType = "role"
        };
    });
builder.Services.AddLogging();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega el JWT obtenido en /auth/login (sin el prefijo 'Bearer')."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement()
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
var MyAllowOrigins = "MyAllowOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(MyAllowOrigins, policy =>
    {
        policy
              // .AllowAnyOrigin()
              //   .AllowAnyMethod()
              //   .AllowAnyHeader()
              .WithOrigins("http://localhost:3000", "https://localhost:3000");
    });
});
var app = builder.Build();

// Create the schema, the roles and the initial users (see Data/IdentitySeeder.cs).
await app.SeedIdentityAsync();

app.UseCors(MyAllowOrigins);
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
//Custom Middleware
// app.Use(async (context, next) =>
// {
//     var path = context.Request.Path;
//     Console.WriteLine($"Request Path: {path}");
//     // Do work that can write to the Response.
//     await next.Invoke();
//     // Do logging or other work that doesn't write to the Response.
//     Console.WriteLine($"Response Status Code: {context.Response.StatusCode}");
// });
app.UseRequestLogging();
app.MapControllers();

app.Run();
