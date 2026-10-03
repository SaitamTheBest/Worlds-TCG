using Microsoft.AspNetCore.Mvc;
using web_tcg.Models.Card;

namespace web_tcg.ViewComponents;

public class CardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CardViewModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        model.Abilities ??= new List<CardAbilityViewModel>();

        return View("Default", model);
    }
}