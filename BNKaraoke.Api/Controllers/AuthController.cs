using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BNKaraoke.Api.DTOs;
using BNKaraoke.Web.DTOs;
using System.Threading.Tasks;
using BNKaraoke.Api.Models;
namespace BNKaraoke.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            var user = new ApplicationUser { UserName = model.PhoneNumber, PhoneNumber = model.PhoneNumber };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRolesAsync(user, model.Roles);
            return Ok(new { message = "User registered successfully!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByNameAsync(model.PhoneNumber);
            if (user == null)
                return Unauthorized("Invalid login attempt.");

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);
            if (!result.Succeeded)
                return Unauthorized("Invalid credentials.");

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new { token = GenerateJwtToken(user), roles });
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            // Placeholder: Implement proper JWT token generation.
            return "example.jwt.token";
        }
    }
}
