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
using BNKaraoke.Api.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using BNKaraoke.Api.Services;
using BNKaraoke.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();
builder.Configuration.AddEnvironmentVariables();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Debug);
    logging.AddFilter("Microsoft.AspNetCore", LogLevel.Debug);
    logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
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
.AddJwtBearer(options =>
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
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("OnTokenValidated event fired.");
            var claims = context.Principal?.Claims;
            if (claims != null)
            {
                logger.LogDebug("Token validated. Claims: {Claims}", string.Join(", ", claims.Select(c => $"{c.Type}: {c.Value}")));
                var subClaim = claims.FirstOrDefault(c => c.Type == "sub")?.Value
                            ?? claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                            ?? claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!string.IsNullOrEmpty(subClaim))
                {
                    logger.LogDebug("Sub claim found: {SubClaim}", subClaim);
                    var identity = context.Principal?.Identity as ClaimsIdentity;
                    if (identity != null && !identity.HasClaim(c => c.Type == ClaimTypes.Name))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Name, subClaim));
                        logger.LogDebug("Added Name claim with value: {SubClaim}", subClaim);
                    }
                }
                else
                {
                    logger.LogWarning("Sub claim not found in token");
                }
            }
            else
            {
                logger.LogWarning("No claims found in token");
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {Exception}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("OnMessageReceived event fired. Token: {Token}", context.Token);
            return Task.CompletedTask;
        }
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
    var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "https://www.bnkaraoke.com", "http://localhost:8080" };
    var loggerFactory = LoggerFactory.Create(logging =>
    {
        logging.AddConsole();
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger<Program>();
    logger.LogInformation("CORS policy configured for origins: {Origins}", string.Join(", ", allowedOrigins));
    options.AddPolicy("AllowNetwork", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddApplicationPart(typeof(EventController).Assembly)
    .AddControllersAsServices();

builder.Services.AddTransient<EventController>();
builder.Services.AddSignalR();
builder.Services.AddScoped<SingerService>();

var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});
var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Starting controller discovery diagnostics...");
var assembly = typeof(EventController).Assembly;
var controllerTypes = assembly.GetTypes()
    .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
    .ToList();
logger.LogInformation("Found {Count} controller types in assembly: {AssemblyName}", controllerTypes.Count, assembly.GetName().Name);
foreach (var controllerType in controllerTypes)
{
    logger.LogInformation("Discovered controller: {ControllerName}", controllerType.Name);
}

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

// Early logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Earliest middleware - Connection received: {Method} {Path} from {RemoteIp}",
        context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    await next.Invoke();
});

// DI failure logging
app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error processing request {Method} {Path}: {Message}",
            context.Request.Method, context.Request.Path, ex.Message);
        throw;
    }
});

// Request logging
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Processing request: {Method} {Path} from Origin: {Origin} with Scheme: {Scheme}",
        context.Request.Method, context.Request.Path, context.Request.Headers["Origin"], context.Request.Scheme);
    await next.Invoke();
    logger.LogDebug("Sending response: {StatusCode} with CORS headers: {AllowOrigin}",
        context.Response.StatusCode, context.Response.Headers["Access-Control-Allow-Origin"]);
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BNKaraoke API v1");
        c.RoutePrefix = "swagger";
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
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowNetwork");
app.UseAuthentication();
app.UseAuthorization();

// Route logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("Before routing: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next.Invoke();
    logger.LogDebug("After routing: {Method} {Path} -> Status: {StatusCode}",
        context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

app.MapControllers();
app.MapHub<SingersHub>("/hubs/singers");

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();

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
        new { PhoneNumber = "1234567891", Roles = new[] {"Singer"}, FirstName = "Singer", LastName = "One" },
        new { PhoneNumber = "1234567895", Roles = new[] {"Song Manager"}, FirstName = "Song", LastName = "Five" }
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
            var result = await userManager.CreateAsync(appUser, "Pwd1234.");
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(appUser, user.Roles);
            }
            else
            {
                app.Logger.LogError("Failed to create user {UserName}: {Errors}", appUser.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

app.Run();