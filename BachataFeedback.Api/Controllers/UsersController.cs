using BachataFeedback.Api.Data;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public UsersController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetUsers()
    {
        var users = await _context.Users
            .Where(u => u.IsActive)
            .Select(u => new UserProfileDto
            {
                Id = u.Id,
                Email = u.Email!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Nickname = u.Nickname,
                StartDancingDate = u.StartDancingDate,
                SelfAssessedLevel = u.SelfAssessedLevel,
                Bio = u.Bio,
                DanceStyles = u.DanceStyles,
                MainPhotoPath = u.MainPhotoPath,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileDto>> GetUser(string id)
    {
        var user = await _context.Users
            .Where(u => u.Id == id && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound();

        var userDto = new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Nickname = user.Nickname,
            StartDancingDate = user.StartDancingDate,
            SelfAssessedLevel = user.SelfAssessedLevel,
            Bio = user.Bio,
            DanceStyles = user.DanceStyles,
            MainPhotoPath = user.MainPhotoPath,
            CreatedAt = user.CreatedAt
        };

        return Ok(userDto);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateProfileDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null || currentUser.Id != id)
            return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Nickname = model.Nickname;
        user.StartDancingDate = model.StartDancingDate;
        user.SelfAssessedLevel = model.SelfAssessedLevel;
        user.Bio = model.Bio;
        user.DanceStyles = model.DanceStyles;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Profile updated successfully" });
    }
}