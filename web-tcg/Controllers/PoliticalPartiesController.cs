using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Admin;
namespace web_tcg.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/PoliticalParties")]
public sealed class PoliticalPartiesController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View("~/Views/Admin/PoliticalParties/Index.cshtml",
        await db.PoliticalParties.AsNoTracking().Include(p => p.Country).OrderBy(p => p.Name).ToListAsync());

    [HttpGet("Create")]
    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Create(int? id)
    {
        var party = id.HasValue ? await db.PoliticalParties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id) : null;
        if (id.HasValue && party == null) return NotFound();
        return await Form(party == null ? new() : new() { Id = party.Id, Name = party.Name,
            CountryId = party.CountryId, Orientation = party.Orientation, ImageUrl = party.ImageUrl });
    }

    [HttpPost("Create")]
    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? id, PoliticalPartyViewModel input)
    {
        input.Id = id;
        var party = id.HasValue ? await db.PoliticalParties.FirstOrDefaultAsync(p => p.Id == id) : null;
        if (id.HasValue && party == null) return NotFound();
        input.Name = (input.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input.Name)) ModelState.AddModelError(nameof(input.Name), "Le nom est obligatoire.");
        if (!await db.Countries.AnyAsync(c => c.Id == input.CountryId)) ModelState.AddModelError(nameof(input.CountryId), "Ce pays n'existe pas.");
        if (await db.PoliticalParties.AnyAsync(p => p.Id != id && p.CountryId == input.CountryId && p.Name.ToLower() == input.Name.ToLower()))
            ModelState.AddModelError(nameof(input.Name), "Un parti de ce nom existe déjà dans ce pays.");
        if (!string.IsNullOrWhiteSpace(input.ImageUrl) && System.Text.RegularExpressions.Regex.IsMatch(input.ImageUrl, @"^/uploads/parties/[a-f0-9]{32}\.(png|jpg|webp)$") &&
            !System.IO.File.Exists(Path.Combine(environment.WebRootPath, input.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))))
            ModelState.AddModelError(nameof(input.ImageUrl), "Le logo n'a pas été enregistré. Téléversez-le à nouveau.");
        if (party != null && party.CountryId != input.CountryId && await db.Cards.AnyAsync(c => c.PoliticalPartyId == party.Id && c.CountryId != input.CountryId))
            ModelState.AddModelError(nameof(input.CountryId), "Ce parti est associé à des cartes d'un autre pays : retirez leur association avant de changer de pays.");
        if (!ModelState.IsValid) return await Form(input);
        party ??= new PoliticalParty();
        party.Name = input.Name; party.CountryId = input.CountryId; party.Orientation = input.Orientation; party.ImageUrl = input.ImageUrl;
        if (!id.HasValue) db.PoliticalParties.Add(party);
        await db.SaveChangesAsync();
        TempData["Success"] = "Parti politique enregistré.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var party = await db.PoliticalParties.FindAsync(id);
        if (party == null) return NotFound();
        if (await db.Cards.AnyAsync(c => c.PoliticalPartyId == id))
        { TempData["Error"] = "Ce parti est associé à des cartes. Retirez ces associations avant de le supprimer."; return RedirectToAction(nameof(Index)); }
        db.PoliticalParties.Remove(party); await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Form(PoliticalPartyViewModel model)
    {
        ViewBag.Countries = new SelectList(await db.Countries.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "Id", "Name", model.CountryId);
        return View("~/Views/Admin/PoliticalParties/Create.cshtml", model);
    }
}
