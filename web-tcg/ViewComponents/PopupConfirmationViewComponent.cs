using Microsoft.AspNetCore.Mvc;

namespace web_tcg.ViewComponents;

public sealed class PopupConfirmationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke() => View();
}
