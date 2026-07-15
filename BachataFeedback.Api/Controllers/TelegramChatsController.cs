using BachataFeedback.Api.Data;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

/// <summary>
/// Управление реестром Telegram-чатов.
/// Чаты регистрируются вручную администратором — указываем chat_id и назначаем роль (purpose).
/// </summary>
[ApiController]
[Route("api/telegram-chats")]
[Authorize(Policy = "AdminOnly")]
public class TelegramChatsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TelegramChatsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/telegram-chats
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var chats = await _db.TelegramChats
            .Include(c => c.DanceGroup)
            .OrderBy(c => c.Purpose)
            .Select(c => new
            {
                c.ChatId,
                c.Type,
                c.Title,
                c.Purpose,
                c.IsActive,
                c.AddedAt,
                DanceGroup = c.DanceGroup == null ? null : new { c.DanceGroup.Id, c.DanceGroup.Name }
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = chats });
    }

    // POST /api/telegram-chats
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterChatRequest req, CancellationToken ct)
    {
        if (!TelegramChatPurpose.All.Contains(req.Purpose))
        {
            return BadRequest(new
            {
                success = false,
                message = $"Invalid purpose. Allowed: {string.Join(", ", TelegramChatPurpose.All)}"
            });
        }

        var existing = await _db.TelegramChats.FindAsync(new object[] { req.ChatId }, ct);
        if (existing != null)
        {
            // Обновляем существующий
            existing.Title = req.Title;
            existing.Purpose = req.Purpose;
            existing.DanceGroupId = req.DanceGroupId;
            existing.IsActive = true;
        }
        else
        {
            _db.TelegramChats.Add(new TelegramChat
            {
                ChatId = req.ChatId,
                Type = req.Type ?? "supergroup",
                Title = req.Title,
                Purpose = req.Purpose,
                DanceGroupId = req.DanceGroupId,
                IsActive = true,
                AddedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // PUT /api/telegram-chats/{chatId}
    [HttpPut("{chatId}")]
    public async Task<IActionResult> Update(long chatId, [FromBody] UpdateChatRequest req, CancellationToken ct)
    {
        var chat = await _db.TelegramChats.FindAsync(new object[] { chatId }, ct);
        if (chat == null) return NotFound(new { success = false, message = "Chat not found" });

        if (req.Purpose != null)
        {
            if (!TelegramChatPurpose.All.Contains(req.Purpose))
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Invalid purpose. Allowed: {string.Join(", ", TelegramChatPurpose.All)}"
                });
            }
            chat.Purpose = req.Purpose;
        }

        if (req.Title != null) chat.Title = req.Title;
        if (req.DanceGroupId.HasValue) chat.DanceGroupId = req.DanceGroupId;
        if (req.IsActive.HasValue) chat.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    // DELETE /api/telegram-chats/{chatId} — деактивирует (не удаляет)
    [HttpDelete("{chatId}")]
    public async Task<IActionResult> Deactivate(long chatId, CancellationToken ct)
    {
        var chat = await _db.TelegramChats.FindAsync(new object[] { chatId }, ct);
        if (chat == null) return NotFound(new { success = false, message = "Chat not found" });

        chat.IsActive = false;
        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true });
    }
}

public record RegisterChatRequest(
    long ChatId,
    string Title,
    string Purpose,
    string? Type,
    int? DanceGroupId);

public record UpdateChatRequest(
    string? Title,
    string? Purpose,
    int? DanceGroupId,
    bool? IsActive);
