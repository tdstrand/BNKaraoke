using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace BNKaraoke.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var firstName = User.FindFirst("firstName")?.Value;
            var lastName = User.FindFirst("lastName")?.Value;
            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            return Ok(new
            {
                UserName = userId,
                FirstName = firstName,
                LastName = lastName,
                Roles = roles
            });
        }
    }
}
