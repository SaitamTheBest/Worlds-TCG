using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Notifications
{
    public sealed class NotificationBellViewModel
    {
        public int UnreadCount { get; init; }
        public IReadOnlyList<NotificationDto> Notifications { get; init; } = [];
    }

    public sealed class NotificationListResponse
    {
        public int UnreadCount { get; init; }
        public IReadOnlyList<NotificationDto> Notifications { get; init; } = [];
    }

    public sealed class SendNotificationRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(600)]
        public string Message { get; set; } = string.Empty;

        public string Type { get; set; } = "Info";

        [MaxLength(500)]
        public string? ActionUrl { get; set; }
    }
}