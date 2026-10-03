using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Models.Card;

namespace web_tcg.Controllers;

[Route("Card")]
public class CardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CardController> _logger;

    public CardController(ApplicationDbContext context, ILogger<CardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Index(int id)
    {
        var card = await _context.Cards
            .AsNoTracking()
            .Include(c => c.CardSet)
            .Include(c => c.Region)
            .Include(c => c.Country)
            .Include(c => c.Variants)
            .Include(c => c.Abilities)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card == null)
            return NotFound();

        var mainVariant = card.Variants
            .OrderBy(v => v.VariantNumber)
            .FirstOrDefault();

        var model = new CardViewModel
        {
            Id = card.Id,
            Name = card.Name,
            Type = card.Type,
            Rarity = card.Rarity,
            UsageCost = card.UsageCost,
            HealthPoints = GetHealthPoints(card),
            ImageUrl = mainVariant?.ImageUrl ?? card.ImageUrl,
            ArtType = mainVariant?.ArtType ?? Data.Enums.CardArtType.Normal,
            CollectionNumber = card.CollectionNumber.ToString(),
            CardSetCode = card.CardSet?.Code ?? string.Empty,
            RegionName = card.Region?.Name ?? string.Empty,
            CountryName = card.Country?.Name ?? string.Empty,
            RegionColor = card.Region?.Color ?? "#FFD700",
            Abilities = card.Abilities
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new CardAbilityViewModel
                {
                    Type = a.Type,
                    Name = a.Name,
                    Description = a.Description,
                    UsageCost = a.UsageCost,
                    DisplayOrder = a.DisplayOrder
                })
                .ToList()
        };

        return ViewComponent("Card", new { model });
    }

    private int? GetHealthPoints(Data.Entities.Card card)
    {
        return card.Type switch
        {   
            Data.Enums.CardType.Leader => _context.LeaderCards
                .Where(lc => lc.CardId == card.Id)
                .Select(lc => lc.HealthPoints)
                .FirstOrDefault(),

            Data.Enums.CardType.Character => _context.CharacterCards
                .Where(cc => cc.CardId == card.Id)
                .Select(cc => cc.HealthPoints)
                .FirstOrDefault(),
            _ => null
        };
    }
}