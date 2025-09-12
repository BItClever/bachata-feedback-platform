namespace BachataFeedback.Api.Authorization;

public static class Permissions
{
    public static class Events
    {
        public const string Create = "events.create";
        public const string Update = "events.update";
        public const string Delete = "events.delete";
    }

    public static class Moderation
    {
        public const string ReviewsResolveReports = "moderation.reviews.resolve";
        public const string EventReviewsResolveReports = "moderation.eventreviews.resolve";
        public const string PhotosResolveReports = "moderation.photos.resolve";
    }

    public static IEnumerable<string> All =>
        new[]
        {
            Events.Create, Events.Update, Events.Delete,
            Moderation.ReviewsResolveReports, Moderation.EventReviewsResolveReports, Moderation.PhotosResolveReports
        };
}

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Organizer = "Organizer";
    public const string User = "User";
}

public static class RolePermissionMap
{
    // Распределение прав по ролям (можно менять и расширять)
    public static IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>
    {
        [SystemRoles.Admin] = new[]
        {
            Permissions.Events.Create, Permissions.Events.Update, Permissions.Events.Delete,
            Permissions.Moderation.ReviewsResolveReports,
            Permissions.Moderation.EventReviewsResolveReports,
            Permissions.Moderation.PhotosResolveReports
        },
        [SystemRoles.Moderator] = new[]
        {
            Permissions.Moderation.ReviewsResolveReports,
            Permissions.Moderation.EventReviewsResolveReports,
            Permissions.Moderation.PhotosResolveReports
        },
        [SystemRoles.Organizer] = new[]
        {
            Permissions.Events.Create, Permissions.Events.Update
        },
        [SystemRoles.User] = Array.Empty<string>()
    };
}