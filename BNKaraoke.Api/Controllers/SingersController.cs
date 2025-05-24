using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BNKaraoke.Api.Services;
using BNKaraoke.Api.Models;

namespace BNKaraoke.Api.Controllers;
[Route("api/singers")]
[ApiController]
[Authorize]
public class SingersController : ControllerBase
{
    private readonly SingerService _singerService;

    public SingersController(SingerService singerService)
    {
        _singerService = singerService;
    }

    [HttpGet("{eventId}")]
    public async Task<ActionResult<List<Singer>>> GetSingers(int eventId)
    {
        var singers = await _singerService.GetSingersAsync(eventId);
        return Ok(singers);
    }
}