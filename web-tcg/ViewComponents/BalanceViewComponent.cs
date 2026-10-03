using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using web_tcg.Data.Entities;
using web_tcg.Models.Balance;

namespace web_tcg.ViewComponents;

public class BalanceViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public BalanceViewComponent(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return Content(string.Empty);

        var userId = UserClaimsPrincipal.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Content(string.Empty);

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
            return Content(string.Empty);

        return View(new BalanceViewModel
        {
            Balance = user.Balance,
            LastBalanceRewardAt = user.LastBalanceRewardAt
        });
    }
}