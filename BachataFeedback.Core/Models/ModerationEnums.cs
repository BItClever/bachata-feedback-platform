namespace BachataFeedback.Core.Models
{
    public enum ModerationLevel
    {
        Pending = 0, // ещё не проверено
        Green = 1, // OK
        Yellow = 2, // возможно токсично — показываем с пометкой
        Red = 3  // токсично — видят только админ/модератор
    }

    public enum ModerationSource
    {
        None = 0,
        LLM = 1,
        Manual = 2
    }
}
