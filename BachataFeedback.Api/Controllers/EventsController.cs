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
public class EventsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public EventsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents()
    {
        var currentUserId = _userManager.GetUserId(User);

        var events = await _context.Events
            .Include(e => e.Creator)
            .Include(e => e.Participants)
            .Select(e => new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Date = e.Date,
                Location = e.Location,
                Description = e.Description,
                CreatedBy = e.CreatedBy,
                CreatorName = e.Creator.FirstName + " " + e.Creator.LastName,
                CreatedAt = e.CreatedAt,
                ParticipantCount = e.Participants.Count,
                IsUserParticipating = currentUserId != null && e.Participants.Any(p => p.UserId == currentUserId)
            })
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EventDto>> GetEvent(int id)
    {
        var currentUserId = _userManager.GetUserId(User);

        var eventEntity = await _context.Events
            .Include(e => e.Creator)
            .Include(e => e.Participants)
            .Where(e => e.Id == id)
            .FirstOrDefaultAsync();

        if (eventEntity == null)
            return NotFound();

        var eventDto = new EventDto
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Date = eventEntity.Date,
            Location = eventEntity.Location,
            Description = eventEntity.Description,
            CreatedBy = eventEntity.CreatedBy,
            CreatorName = eventEntity.Creator.FirstName + " " + eventEntity.Creator.LastName,
            CreatedAt = eventEntity.CreatedAt,
            ParticipantCount = eventEntity.Participants.Count,
            IsUserParticipating = currentUserId != null && eventEntity.Participants.Any(p => p.UserId == currentUserId)
        };

        return Ok(eventDto);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<EventDto>> CreateEvent([FromBody] CreateEventDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var eventEntity = new Event
        {
            Name = model.Name,
            Date = model.Date,
            Location = model.Location,
            Description = model.Description,
            CreatedBy = currentUser.Id
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Автоматически добавляем создателя как участника
        var participation = new EventParticipant
        {
            UserId = currentUser.Id,
            EventId = eventEntity.Id,
            IsConfirmed = true
        };

        _context.EventParticipants.Add(participation);
        await _context.SaveChangesAsync();

        var eventDto = new EventDto
        {
            Id = eventEntity.Id,
            Name = eventEntity.Name,
            Date = eventEntity.Date,
            Location = eventEntity.Location,
            Description = eventEntity.Description,
            CreatedBy = eventEntity.CreatedBy,
            CreatorName = currentUser.FirstName + " " + currentUser.LastName,
            CreatedAt = eventEntity.CreatedAt,
            ParticipantCount = 1,
            IsUserParticipating = true
        };

        return CreatedAtAction(nameof(GetEvent), new { id = eventEntity.Id }, eventDto);
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> JoinEvent(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            return NotFound();

        var existingParticipation = await _context.EventParticipants
            .FirstOrDefaultAsync(ep => ep.EventId == id && ep.UserId == currentUser.Id);

        if (existingParticipation != null)
            return BadRequest(new { message = "Already participating in this event" });

        var participation = new EventParticipant
        {
            UserId = currentUser.Id,
            EventId = id,
            IsConfirmed = false
        };

        _context.EventParticipants.Add(participation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Successfully joined the event" });
    }

    [HttpDelete("{id}/leave")]
    [Authorize]
    public async Task<IActionResult> LeaveEvent(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var participation = await _context.EventParticipants
            .FirstOrDefaultAsync(ep => ep.EventId == id && ep.UserId == currentUser.Id);

        if (participation == null)
            return BadRequest(new { message = "Not participating in this event" });

        _context.EventParticipants.Remove(participation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Successfully left the event" });
    }
}