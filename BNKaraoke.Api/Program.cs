using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BNKaraoke.Api.Models;
using BNKaraoke.Api.Data;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();
builder.Configuration.AddEnvironmentVariables();

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7290, listenOptions =>
    {
        if (builder.Environment.IsDevelopment())
        {
            listenOptions.UseHttps();
        }
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string 'DefaultConnection' is missing.");
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var keyString = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrEmpty(keyString) || Encoding.UTF8.GetBytes(keyString).Length < 32)
{
    throw new InvalidOperationException("Jwt secret key is missing or too short. It must be at least 256 bits (32+ characters).");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString)),
        NameClaimType = "sub"
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Singer", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Singer");
    });
    options.AddPolicy("SongManager", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Song Manager");
    });
    options.AddPolicy("UserManager", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("User Manager");
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetwork", policy =>
    {
        policy.WithOrigins("http://localhost:8080", "https://www.bnkaraoke.com", "https://bnkaraoke.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
        Console.WriteLine("CORS policy configured for http://localhost:8080, https://www.bnkaraoke.com, https://bnkaraoke.com");
    });
});

builder.Services.AddControllers();

// Add Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BNKaraoke API",
        Version = "v1",
        Description = "API for managing karaoke users and songs"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BNKaraoke API v1");
        c.RoutePrefix = string.Empty;
    });
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var error = context.Features.Get<IExceptionHandlerFeature>();
            logger.LogError(error?.Error, "Unhandled exception at {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
        });
    });
    app.UseHsts();
}

app.UseRouting();
app.UseCors("AllowNetwork");
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request Incoming: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next.Invoke();
    logger.LogInformation("Response Sent: {StatusCode}", context.Response.StatusCode);
});

app.MapControllers();

// Seed roles and users
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var roles = new[] { "Singer", "Karaoke DJ", "User Manager", "Queue Manager", "Song Manager", "Event Manager", "Application Manager" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var users = new[]
    {
        new { PhoneNumber = "12345678901", Roles = new[] {"Singer"}, FirstName = "Singer", LastName = "One" },
        new { PhoneNumber = "12345678905", Roles = new[] {"Song Manager"}, FirstName = "Song", LastName = "Five" }
    };

    foreach (var user in users)
    {
        var appUser = new ApplicationUser
        {
            UserName = user.PhoneNumber,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        if (await userManager.FindByNameAsync(appUser.UserName) == null)
        {
            var result = await userManager.CreateAsync(appUser, "Pwd123.");
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(appUser, user.Roles);
            }
        }
    }
}

app.Run();