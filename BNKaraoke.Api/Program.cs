using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BNKaraoke.Api.Models;
using BNKaraoke.Api.Data;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel based on environment
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(7290, listenOptions =>
        {
            listenOptions.UseHttps(); // Uses dev cert (dotnet dev-certs)
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });
        Console.WriteLine("Kestrel configured to listen on https://localhost:7290 in Development");
    });
}
else
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenLocalhost(7290, listenOptions =>
        {
            listenOptions.UseHttps(); // Production cert handled elsewhere
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });
        Console.WriteLine("Kestrel configured to listen on https://localhost:7290 in Production");
    });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = false;
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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            Console.WriteLine($"Unhandled exception at {context.Request.Path}: {context.Response.StatusCode}");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"An unexpected error occurred.\"}");
        });
    });
    app.UseHsts();
}

app.UseRouting();
app.UseCors("AllowNetwork");
Console.WriteLine("CORS policy 'AllowNetwork' applied");
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    Console.WriteLine($"Request Incoming: {context.Request.Method} {context.Request.Path}");
    await next.Invoke();
    Console.WriteLine($"Response Sent: {context.Response.StatusCode}");
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
            var result = await userManager.CreateAsync(appUser, "pwd");
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(appUser, user.Roles);
            }
        }
    }
}

app.Run();