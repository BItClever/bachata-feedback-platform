using BachataFeedback.Api.Data;
using BachataFeedback.Api.Services;
using BachataFeedback.Api.Services.Storage;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BachataFeedback.Api.Controllers;

/// <summary>
/// Telegram Login Widget flow:
/// 1. Фронт отображает виджет, пользователь нажимает "Войти через Telegram"
/// 2. Telegram редиректит на наш callback-URL с подписанными параметрами
/// 3. Этот контроллер верифицирует HMAC-SHA256 и выдаёт наш JWT
/// </summary>
[ApiController]
[Route("api/auth/telegram")]
[EnableRateLimiting("auth")]
public class TelegramAuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ITokenService _tokenService;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IStorageService _storage;
    private readonly ILogger<TelegramAuthController> _logger;

    // Telegram рекомендует проверять auth_date не старше 86400 секунд (24 часа).
    // Слишком короткий интервал (10 мин) ломает UX, если пользователь открыл страницу и
    // вернулся позже.
    private const int MaxAuthAgeSeconds = 86400;

    public TelegramAuthController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ITokenService tokenService,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IStorageService storage,
        ILogger<TelegramAuthController> logger)
    {
        _context = context;
        _userManager = userManager;
        _tokenService = tokenService;
        _roleManager = roleManager;
        _configuration = configuration;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/auth/telegram/callback
    /// Принимает данные от Telegram Login Widget, верифицирует и логинит/регистрирует пользователя.
    /// </summary>
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] TelegramAuthDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 1. Верифицируем подпись
        var botToken = _configuration["Telegram:BotToken"];
        if (string.IsNullOrEmpty(botToken))
        {
            _logger.LogError("Telegram:BotToken is not configured");
            return StatusCode(503, new { success = false, message = "Telegram authentication is not configured" });
        }

        if (!VerifyTelegramAuth(model, botToken, out var errorMsg))
        {
            _logger.LogWarning("Telegram auth verification failed: {Error}. AuthDate={AuthDate}, Now={Now}",
                errorMsg, model.AuthDate, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return BadRequest(new { success = false, message = errorMsg });
        }

        // 2. Ищем существующего пользователя по TelegramId
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == model.Id);

        User user;
        bool isNewUser = false;

        if (existingUser != null)
        {
            // Обновляем username на случай если изменился
            if (existingUser.TelegramUsername != model.Username)
            {
                existingUser.TelegramUsername = model.Username;
                await _context.SaveChangesAsync();
            }
            user = existingUser;
        }
        else
        {
            // Новый пользователь — регистрируем автоматически
            isNewUser = true;

            // Генерируем уникальный email-заглушку (Identity требует уникальный UserName)
            // Формат: tg_{telegramId}@tg.internal — не отправляется на почту
            var fakeEmail = $"tg_{model.Id}@tg.internal";
            var firstName = model.FirstName ?? "Telegram";
            var lastName = model.LastName ?? "User";

            user = new User
            {
                UserName = fakeEmail,
                Email = fakeEmail,
                EmailConfirmed = true, // не нужно подтверждать
                FirstName = firstName,
                LastName = lastName,
                Nickname = model.Username,
                TelegramId = model.Id,
                TelegramUsername = model.Username,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create Telegram user {TelegramId}: {Errors}", model.Id, errors);
                return BadRequest(new { success = false, message = errors });
            }

            // Назначаем базовую роль
            if (await _roleManager.RoleExistsAsync("User"))
                await _userManager.AddToRoleAsync(user, "User");

            // Скачиваем фото профиля из Telegram и сохраняем в MinIO (fire-and-forget, не блокируем логин)
            if (!string.IsNullOrEmpty(model.PhotoUrl))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var http = new HttpClient();
                        http.Timeout = TimeSpan.FromSeconds(10);
                        var photoBytes = await http.GetByteArrayAsync(model.PhotoUrl);

                        var ext = "jpg";
                        var key = $"users/{user.Id}/tg_photo.{ext}";

                        using var stream = new MemoryStream(photoBytes);
                        await _storage.PutObjectAsync(key, stream, "image/jpeg");

                        // Обновляем путь к фото в профиле через отдельный scope
                        user.MainPhotoPath = key;
                        await _userManager.UpdateAsync(user);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download Telegram profile photo for user {UserId}", user.Id);
                    }
                });
            }
        }

        var token = await _tokenService.GenerateTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            success = true,
            token,
            isNewUser,
            user = new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Nickname,
                user.TelegramId,
                user.TelegramUsername,
                user.StartDancingDate,
                user.SelfAssessedLevel,
                user.Bio,
                user.DanceStyles,
                user.MainPhotoPath,
                user.CreatedAt,
                user.DancerRole,
                Roles = roles,
                Permissions = Array.Empty<string>()
            }
        });
    }

    /// <summary>
    /// GET /api/auth/telegram/bot-name
    /// Возвращает имя бота для инициализации виджета на фронте (без секрета)
    /// </summary>
    [HttpGet("bot-name")]
    public IActionResult GetBotName()
    {
        var botName = _configuration["Telegram:BotName"];
        if (string.IsNullOrEmpty(botName))
            return StatusCode(503, new { success = false, message = "Telegram bot not configured" });

        return Ok(new { botName });
    }

    // --- Верификация Telegram Login Widget ---

    private static bool VerifyTelegramAuth(TelegramAuthDto data, string botToken, out string errorMessage)
    {
        // Проверка свежести auth_date (не старше 10 минут)
        var authDate = DateTimeOffset.FromUnixTimeSeconds(data.AuthDate);
        if (DateTimeOffset.UtcNow - authDate > TimeSpan.FromSeconds(MaxAuthAgeSeconds))
        {
            errorMessage = "Telegram auth data is expired";
            return false;
        }

        // Формируем data_check_string: все поля кроме hash, отсортированные по ключу, через \n
        var fields = new SortedDictionary<string, string>();
        fields["auth_date"] = data.AuthDate.ToString();
        fields["id"] = data.Id.ToString();
        if (!string.IsNullOrEmpty(data.FirstName)) fields["first_name"] = data.FirstName;
        if (!string.IsNullOrEmpty(data.LastName)) fields["last_name"] = data.LastName;
        if (!string.IsNullOrEmpty(data.Username)) fields["username"] = data.Username;
        if (!string.IsNullOrEmpty(data.PhotoUrl)) fields["photo_url"] = data.PhotoUrl;

        var dataCheckString = string.Join("\n", fields.Select(kv => $"{kv.Key}={kv.Value}"));

        // secret_key = SHA256(bot_token) — для Login Widget (не для WebApp!)
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));

        // Вычисляем HMAC-SHA256
        using var hmac = new HMACSHA256(secretKey);
        var computedHash = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))
        ).ToLowerInvariant();

        if (!string.Equals(computedHash, data.Hash, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Invalid Telegram auth hash";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}

public class TelegramAuthDto
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? PhotoUrl { get; set; }
    public long AuthDate { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string Hash { get; set; } = string.Empty;
}
