using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BNKaraoke.Api.Models;
using BNKaraoke.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel and URLs (only for development)
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseKestrel().UseUrls("https://localhost:7280;http://localhost:5176");
}

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Identity & Authentication
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Enable CORS for React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Validate JWT Key Length & Configure Authentication
var keyString = builder.Configuration["JwtSettings:SecretKey"];

if (string.IsNullOrEmpty(keyString) || Encoding.UTF8.GetBytes(keyString).Length < 32)
{
    throw new InvalidOperationException("Jwt secret key is missing or too short. It must be at least 256 bits (32+ characters).");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = "BNKaraokeCookie";
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString))
    };
});

// Register MVC Controllers & Razor Pages
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // Ensure API controllers are registered

var app = builder.Build();

// Configure Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers(); // Registers API controllers properly
app.MapRazorPages();
app.MapDefaultControllerRoute(); // Ensures default controller behavior

// Seed Roles & Users
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
        new { PhoneNumber = "12345678901", Role = "Singer", FirstName = "Singer", LastName = "One" },
        new { PhoneNumber = "12345678902", Role = "Karaoke DJ", FirstName = "DJ", LastName = "Two" },
        new { PhoneNumber = "12345678903", Role = "User Manager", FirstName = "Manager", LastName = "Three" },
        new { PhoneNumber = "12345678904", Role = "Queue Manager", FirstName = "Queue", LastName = "Four" },
        new { PhoneNumber = "12345678905", Role = "Song Manager", FirstName = "Song", LastName = "Five" },
        new { PhoneNumber = "12345678906", Role = "Event Manager", FirstName = "Event", LastName = "Six" },
        new { PhoneNumber = "12345678907", Role = "Application Manager", FirstName = "Application", LastName = "Seven" }
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
                await userManager.AddToRoleAsync(appUser, user.Role);
            }
        }
    }
}

app.Run();
