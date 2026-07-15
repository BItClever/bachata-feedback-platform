namespace BachataFeedback.TelegramBot.Services;

/// <summary>
/// Конфигурация бота: токен и список telegram_user_id администраторов.
/// Администраторы могут использовать admin-команды (создание занятий, публикация poll'ов и т.д.).
/// Синхронизируется с ролями платформы через TelegramId в таблице Users.
/// </summary>
public class BotConfiguration
{
    public string BotToken { get; }

    /// <summary>
    /// Telegram user_id пользователей, которым разрешены admin-команды.
    /// Задаются в конфиге Telegram:AdminUserIds.
    /// </summary>
    public long[] AdminUserIds { get; }

    public BotConfiguration(string botToken, long[] adminUserIds)
    {
        BotToken = botToken;
        AdminUserIds = adminUserIds;
    }

    public bool IsAdmin(long telegramUserId) =>
        AdminUserIds.Contains(telegramUserId);
}
