
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QFamilyAPI.Data;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONTROLLERS
// ==========================================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

// ==========================================
// 2. SQL SERVER
// ==========================================

builder.Services.AddDbContext<QFamilyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
    )
);

// ==========================================
// 3. JWT CONFIGURATION
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Missing JWT key.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Missing JWT issuer.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Missing JWT audience.");

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,

                ValidateAudience = true,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,

                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ClockSkew = TimeSpan.Zero
            };

        // ==================================
        // 4. KIEM TRA TAI KHOAN ISACTIVE
        // ==================================

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim =
                    context.Principal?.FindFirst(
                        ClaimTypes.NameIdentifier
                    )?.Value;

                if (!int.TryParse(
                    userIdClaim,
                    out var userId))
                {
                    context.Fail(
                        "Invalid user identifier.");

                    return;
                }

                var dbContext =
                    context.HttpContext.RequestServices
                        .GetRequiredService<
                            QFamilyDbContext>();

                var user = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Role,
                        u.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    context.Fail(
                        "User does not exist.");

                    return;
                }

                if (!user.IsActive)
                {
                    context.Fail(
                        "User account is disabled.");

                    return;
                }

                // Dam bao Role trong JWT
                // van trung voi database
                if (!context.Principal!.IsInRole(
                    user.Role))
                {
                    context.Fail(
                        "User role has changed.");

                    return;
                }
            }
        };
    });

// ==========================================
// 5. AUTHORIZATION
// ==========================================

builder.Services.AddAuthorization();

// ==========================================
// 6. SWAGGER JWT
// ==========================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "QFamilyAPI",
            Version = "v1"
        });

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",

            Type = SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT",

            In = ParameterLocation.Header,

            Description =
                "Nhap JWT Access Token"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,

                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

// ==========================================
// 7. BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// 8. SWAGGER
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

// ==========================================
// 9. MIDDLEWARE
// ==========================================

app.UseHttpsRedirection();

// Xac thuc JWT
app.UseAuthentication();

// Kiem tra quyen truy cap
app.UseAuthorization();

// ==========================================
// 10. API CONTROLLERS
// ==========================================

app.MapControllers();

// ==========================================
// 11. RUN
// ==========================================

app.Run();
