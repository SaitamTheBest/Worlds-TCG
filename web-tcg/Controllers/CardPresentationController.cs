using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Services;

namespace web_tcg.Controllers;

[Authorize]
[Route("Card/Render")]
public sealed class CardPresentationController(CardPresentationService presentation) : Controller
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Render(int id)
    {
        var card = (await presentation.GetAsync(new[] { id })).FirstOrDefault();
        return card == null ? NotFound() : ViewComponent("Card", new { model = card });
    }
}
