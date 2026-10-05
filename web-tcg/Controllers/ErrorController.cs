using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using web_tcg.Models;

namespace web_tcg.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    // Accepte aussi les requêtes POST réexécutées après une erreur.
    [Route("/Error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index(int statusCode = 500)
    {
        if (statusCode < 400 || statusCode > 599) statusCode = 500;
        Response.StatusCode = statusCode;
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            StatusCode = statusCode,
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
