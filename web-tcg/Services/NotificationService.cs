using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Data.Enums;
using web_tcg.Hubs;
using web_tcg.Models.Notifications;

namespace web_tcg.Services
{
    public sealed class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<NotificationDto> CreateAsync(
            string userId,
            string title,
            string message,
            NotificationType type = NotificationType.Info,
            string? actionUrl = null,
            CancellationToken cancellationToken = default)
        {
            var notification = AddPending(userId, title, message, type, actionUrl);
            await _context.SaveChangesAsync(cancellationToken);
            await PublishAsync(notification, cancellationToken);
            return ToDto(notification);
        }

        // Permet aux opérations métier de sauvegarder la notification dans leur transaction.
        public Notification AddPending(string userId, string title, string message,
            NotificationType type = NotificationType.Info, string? actionUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("L'identifiant utilisateur est requis.", nameof(userId));

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Le titre est requis.", nameof(title));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Le message est requis.", nameof(message));

            var notification = new Notification
            {
                UserId = userId,
                Title = title.Trim(),
                Message = message.Trim(),
                Type = type,
                ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            return notification;
        }

        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
            => _hubContext.Clients
                .User(notification.UserId)
                .SendAsync(
                    "NotificationCreated",
                    ToDto(notification),
                    cancellationToken);

        public async Task<IReadOnlyList<NotificationDto>> GetLatestAsync(
            string userId,
            int take = 10,
            CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 30);

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);

            return notifications
                .Select(ToDto)
                .ToList();
        }

        public Task<int> GetUnreadCountAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return _context.Notifications
                .AsNoTracking()
                .CountAsync(
                    x => x.UserId == userId && !x.IsRead,
                    cancellationToken);
        }

        public async Task<bool> MarkAsReadAsync(
            int notificationId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(
                    x => x.Id == notificationId && x.UserId == userId,
                    cancellationToken);

            if (notification == null)
                return false;

            if (notification.IsRead)
                return true;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<int> MarkAllAsReadAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            return await _context.Notifications
                .Where(x => x.UserId == userId && !x.IsRead)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.IsRead, true)
                        .SetProperty(x => x.ReadAt, now),
                    cancellationToken);
        }

        private static NotificationDto ToDto(Notification notification)
        {
            return new NotificationDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type.ToString().ToLowerInvariant(),
                ActionUrl = notification.ActionUrl,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            };
        }
    }
}
