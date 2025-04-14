using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BNKaraoke.Api.DTOs;
using BNKaraoke.Api.Models;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

namespace BNKaraoke.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            _logger.LogInformation("Test endpoint called");
            return Ok("API is working!");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            _logger.LogInformation("Register: Received payload - {Payload}", JsonConvert.SerializeObject(model));
            if (model == null || string.IsNullOrEmpty(model.PhoneNumber))
            {
                _logger.LogWarning("Register: Model or PhoneNumber is null");
                return BadRequest(new { error = "PhoneNumber is required" });
            }

            var user = new ApplicationUser
            {
                UserName = model.PhoneNumber,
                PhoneNumber = model.PhoneNumber,
                NormalizedUserName = _userManager.NormalizeName(model.PhoneNumber),
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = true
            };
            _logger.LogInformation("Register: Creating user - UserName: {UserName}, PhoneNumber: {PhoneNumber}", user.UserName, user.PhoneNumber);

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                _logger.LogError("Register: Failed to create user - Errors: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Registration failed", details = result.Errors });
            }

            var createdUser = await _userManager.FindByNameAsync(model.PhoneNumber);
            if (createdUser == null)
            {
                _logger.LogError("Register: Failed to find newly created user - UserName: {UserName}", model.PhoneNumber);
                return StatusCode(500, new { error = "Failed to find created user" });
            }
            await _userManager.AddToRolesAsync(createdUser, model.Roles);
            _logger.LogInformation("Register: User {UserName} registered with roles: {Roles}", createdUser.UserName, string.Join(", ", model.Roles));
            return Ok(new { message = "User registered successfully!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            _logger.LogInformation("Login attempt: UserName={UserName}", model.UserName);

            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null)
            {
                _logger.LogWarning("Login: User not found - UserName={UserName}", model.UserName);
                return Unauthorized(new { error = "Invalid login attempt." });
            }

            _logger.LogInformation("Login: User found. Checking password...");
            var isPasswordValid = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!isPasswordValid.Succeeded)
            {
                _logger.LogWarning("Login: Invalid password for UserName={UserName}", model.UserName);
                return Unauthorized(new { error = "Invalid credentials." });
            }

            _logger.LogInformation("Login: Password validated. Retrieving roles...");
            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation("Login: Roles retrieved. Generating token...");
            var token = GenerateJwtToken(user, roles);

            _logger.LogInformation("Login successful for UserName={UserName}", model.UserName);
            return Ok(new { message = "Success", token, firstName = user.FirstName, lastName = user.LastName, roles });
        }

        [HttpGet("users")]
        [Authorize(Policy = "UserManager")]
        public async Task<IActionResult> GetUsers()
        {
            var users = _userManager.Users.ToList();
            var userList = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new
                {
                    id = user.Id,
                    userName = user.UserName,
                    email = user.Email ?? "N/A",
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    roles = roles.ToArray()
                });
            }
            _logger.LogInformation("Returning {UserCount} users", userList.Count);
            return Ok(userList);
        }

        [HttpGet("roles")]
        [Authorize(Policy = "UserManager")]
        public IActionResult GetRoles()
        {
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            _logger.LogInformation("Returning {RoleCount} roles", roles.Count);
            return Ok(roles);
        }

        [HttpPost("assign-roles")]
        [Authorize(Policy = "UserManager")]
        public async Task<IActionResult> AssignRoles([FromBody] AssignRolesDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("AssignRoles: User not found - UserId={UserId}", model.UserId);
                return NotFound(new { error = "User not found" });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(r => !model.Roles.Contains(r)).ToList();
            var rolesToAdd = model.Roles.Where(r => !currentRoles.Contains(r)).ToList();

            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                _logger.LogError("AssignRoles: Failed to remove roles for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", removeResult.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to remove roles", details = removeResult.Errors });
            }

            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                _logger.LogError("AssignRoles: Failed to add roles for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", addResult.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to add roles", details = addResult.Errors });
            }

            _logger.LogInformation("Assigned roles to {UserName}: {Roles}", user.UserName, string.Join(", ", model.Roles));
            return Ok(new { message = "Roles assigned successfully" });
        }

        [HttpPost("delete-user")]
        [Authorize(Policy = "UserManager")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("DeleteUser: User not found - UserId={UserId}", model.UserId);
                return NotFound(new { error = "User not found" });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("DeleteUser: Failed to delete user UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to delete user", details = result.Errors });
            }

            _logger.LogInformation("Deleted user: {UserName}", user.UserName);
            return Ok(new { message = "User deleted successfully" });
        }

        [HttpPost("reset-password")]
        [Authorize(Policy = "UserManager")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("ResetPassword: User not found - UserId={UserId}", model.UserId);
                return NotFound(new { error = "User not found" });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("ResetPassword: Failed to reset password for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to reset password", details = result.Errors });
            }

            _logger.LogInformation("Reset password for user: {UserName}", user.UserName);
            return Ok(new { message = "Password reset successfully" });
        }

        [HttpPost("update-user")]
        [Authorize(Policy = "UserManager")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("UpdateUser: User not found - UserId={UserId}", model.UserId);
                return NotFound(new { error = "User not found" });
            }

            user.UserName = model.UserName;
            user.PhoneNumber = model.UserName;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("UpdateUser: Failed to update user UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to update user details", details = updateResult.Errors });
            }

            if (!string.IsNullOrEmpty(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);
                if (!passwordResult.Succeeded)
                {
                    _logger.LogError("UpdateUser: Failed to update password for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", passwordResult.Errors.Select(e => e.Description)));
                    return BadRequest(new { error = "Failed to update password", details = passwordResult.Errors });
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(r => !model.Roles.Contains(r)).ToList();
            var rolesToAdd = model.Roles.Where(r => !currentRoles.Contains(r)).ToList();

            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                _logger.LogError("UpdateUser: Failed to remove roles for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", removeResult.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to remove roles", details = removeResult.Errors });
            }

            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                _logger.LogError("UpdateUser: Failed to add roles for UserId={UserId} - Errors: {Errors}", model.UserId, string.Join(", ", addResult.Errors.Select(e => e.Description)));
                return BadRequest(new { error = "Failed to add roles", details = addResult.Errors });
            }

            _logger.LogInformation("Updated user: {UserName}", user.UserName);
            return Ok(new { message = "User updated successfully" });
        }

        private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
        {
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var keyString = _configuration["JwtSettings:SecretKey"];

            if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience) || string.IsNullOrEmpty(keyString))
            {
                _logger.LogError("GenerateJwtToken: Missing JWT configuration");
                throw new InvalidOperationException("JWT configuration is missing.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName ?? throw new InvalidOperationException("UserName cannot be null")),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("firstName", user.FirstName ?? string.Empty),
                new Claim("lastName", user.LastName ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class AssignRolesDto
    {
        public required string UserId { get; set; }
        public required string[] Roles { get; set; }
    }

    public class DeleteUserDto
    {
        public required string UserId { get; set; }
    }

    public class ResetPasswordDto
    {
        public required string UserId { get; set; }
        public required string NewPassword { get; set; }
    }

    public class UpdateUserDto
    {
        public required string UserId { get; set; }
        public required string UserName { get; set; }
        public string? Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string[] Roles { get; set; }
    }
}