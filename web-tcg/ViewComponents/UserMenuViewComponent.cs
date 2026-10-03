using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Data.Entities;
using web_tcg.Models.Profile;

namespace web_tcg.ViewComponents
{
    public sealed class UserMenuViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserMenuViewComponent(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user =
                await _userManager.GetUserAsync(
                    UserClaimsPrincipal);

            if (user == null)
                return Content(string.Empty);

            return View(
                new UserMenuViewModel
                {
                    Username =
                        user.UserName ?? string.Empty,

                    AvatarUrl =
                        user.AvatarUrl,

                    IsAdmin =
                        UserClaimsPrincipal.IsInRole("Admin")
                });
        }
    }
}