using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Data.Enums;
using web_tcg.Models.Notifications;
using web_tcg.Services;

namespace web_tcg.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/notifications")]
    public sealed class NotificationsController : ControllerBase
    {
        private readonly NotificationService _notificationService;

        public NotificationsController(
            NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<ActionResult<NotificationListResponse>> GetNotifications(
            [FromQuery] int take = 10,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var notifications =
                await _notificationService.GetLatestAsync(
                    userId,
                    take,
                    cancellationToken);

            var unreadCount =
                await _notificationService.GetUnreadCountAsync(
                    userId,
                    cancellationToken);

            return Ok(new NotificationListResponse
            {
                Notifications = notifications,
                UnreadCount = unreadCount
            });
        }

        [HttpPost("{id:int}/read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(
            int id,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var result =
                await _notificationService.MarkAsReadAsync(
                    id,
                    userId,
                    cancellationToken);

            if (!result)
                return NotFound();

            return Ok(new
            {
                success = true
            });
        }

        [HttpPost("read-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();

            if (userId == null)
                return Unauthorized();

            var count =
                await _notificationService.MarkAllAsReadAsync(
                    userId,
                    cancellationToken);

            return Ok(new
            {
                success = true,
                count
            });
        }

        [HttpPost("send")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<NotificationDto>> Send(
            [FromBody] SendNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<NotificationType>(
                    request.Type,
                    true,
                    out var type))
            {
                type = NotificationType.Info;
            }

            var notification =
                await _notificationService.CreateAsync(
                    request.UserId,
                    request.Title,
                    request.Message,
                    type,
                    request.ActionUrl,
                    cancellationToken);

            return Ok(notification);
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(
                ClaimTypes.NameIdentifier);
        }
    }
}