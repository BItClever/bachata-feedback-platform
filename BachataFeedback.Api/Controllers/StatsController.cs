using BachataFeedback.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class StatsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public StatsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var totalUsers = await _db.Users.CountAsync(u => u.IsActive);
        var totalReviews = await _db.Reviews.CountAsync();
        var totalEventReviews = await _db.EventReviews.CountAsync();
        var totalEvents = await _db.Events.CountAsync();

        return Ok(new
        {
            totalUsers,
            totalReviews,
            totalEventReviews,
            totalEvents
        });
    }
}