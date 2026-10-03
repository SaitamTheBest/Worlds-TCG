using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Data.Entities;
using web_tcg.Models.Balance;

namespace web_tcg.ViewComponents
{
    public sealed class CardBalanceViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CardBalanceViewComponent(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(
                UserClaimsPrincipal);

            if (user == null)
                return Content(string.Empty);

            return View(
                new CardBalanceViewModel
                {
                    CardBalance = user.CardBalance
                });
        }
    }
}