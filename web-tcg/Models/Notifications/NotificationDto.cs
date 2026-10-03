namespace web_tcg.Models.Notifications
{
    public sealed class NotificationDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Type { get; init; } = "info";
        public string? ActionUrl { get; init; }
        public bool IsRead { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}