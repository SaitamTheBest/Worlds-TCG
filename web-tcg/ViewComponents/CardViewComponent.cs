using Microsoft.AspNetCore.Mvc;
using web_tcg.Models.Card;

namespace web_tcg.ViewComponents;

public class CardViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CardViewModel? model = null, bool faceDown = false)
    {
        // Un dos est indépendant de la carte cachée : aucun nom, rareté ou effet requis.
        if (faceDown)
            return View("Back");

        if (model == null)
            throw new ArgumentNullException(nameof(model));

        model.Abilities ??= new List<CardAbilityViewModel>();

        return View("Default", model);
    }
}
