using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Models.Notifications;
using web_tcg.Services;

namespace web_tcg.ViewComponents
{
    public sealed class NotificationViewComponent : ViewComponent
    {
        private readonly NotificationService _notificationService;

        public NotificationViewComponent(
            NotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId =
                UserClaimsPrincipal.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Content(string.Empty);

            var notifications =
                await _notificationService.GetLatestAsync(
                    userId,
                    10);

            var unreadCount =
                await _notificationService.GetUnreadCountAsync(
                    userId);

            return View(new NotificationBellViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount
            });
        }
    }
}