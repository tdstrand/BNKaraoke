using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BNKaraoke.Api.DTOs;
using BNKaraoke.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("API is working!");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            var user = new ApplicationUser
            {
                UserName = model.PhoneNumber,
                PhoneNumber = model.PhoneNumber,
                FirstName = model.FirstName, // ✅ Store First Name
                LastName = model.LastName     // ✅ Store Last Name
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(new { message = "Registration failed.", errors = result.Errors });

            await _userManager.AddToRolesAsync(user, model.Roles);
            return Ok(new { message = "User registered successfully!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            Console.WriteLine($"Login attempt: UserName={model.UserName}");

            var user = await _userManager.FindByNameAsync(model.UserName); // ✅ Retrieve full user object

            if (user == null)
            {
                Console.WriteLine("User not found.");
                return Unauthorized(new { message = "Invalid login attempt." });
            }

            Console.WriteLine("User found. Checking password...");

            var isPasswordValid = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!isPasswordValid.Succeeded)
            {
                Console.WriteLine("Invalid password.");
                return Unauthorized(new { message = "Invalid credentials." });
            }

            Console.WriteLine("Password validated. Retrieving roles...");
            var roles = await _userManager.GetRolesAsync(user);

            Console.WriteLine("Roles retrieved. Generating token...");
            var token = GenerateJwtToken(user, roles);

            Console.WriteLine($"Login successful! Returning token: {token}");

            // ✅ Return FirstName & LastName with login response
            return Ok(new { message = "Success", token, firstName = user.FirstName, lastName = user.LastName, roles });
        }

        private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("firstName", user.FirstName), // ✅ Include FirstName in token claims
                new Claim("lastName", user.LastName) // ✅ Include LastName in token claims
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
