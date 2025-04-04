using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BNKaraoke.Api.Models;
using BNKaraoke.Api.Data;

using BNKaraoke.Api.Models;
var builder = WebApplication.CreateBuilder(args);

// **Configure Database**
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// **Configure Identity & Authentication**
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

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
    var keyString = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt key is not set.");
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString))
    };
});

// **Register MVC Controllers & Razor Pages**
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// **Configure Middleware**
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
app.UseAuthentication();
app.UseAuthorization();

// **Define Route Mappings**
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "userManagement",
    pattern: "{controller=UserManagement}/{action=Index}/{id?}");

// **Seed Roles & Users in an Async Manner**
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
