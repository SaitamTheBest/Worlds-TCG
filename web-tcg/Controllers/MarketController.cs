using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Market;
using web_tcg.Services;

namespace web_tcg.Controllers;

[Authorize]
[Route("Market")]
public sealed class MarketController(ApplicationDbContext context, MarketService market, CardPresentationService presentation) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("")]
    public async Task<IActionResult> Index(string search = "", int page = 1) => View("Index", await ListAsync("market",search,page));
    [HttpGet("MySales")]
    public async Task<IActionResult> MySales(int page = 1) => View("Index", await ListAsync("sales","",page));
    [HttpGet("History")]
    public async Task<IActionResult> History(int page = 1) => View("Index", await ListAsync("history","",page));
    [HttpGet("Listings")]
    public async Task<IActionResult> Listings(string mode = "market", string search = "", int page = 1)
        => PartialView("_Listings", await ListAsync(mode,search,page));

    private async Task<MarketIndexViewModel> ListAsync(string mode, string search, int page)
    {
        mode = mode is "sales" or "history" ? mode : "market";
        page = Math.Clamp(page,1,100000);
        search = (search ?? "").Trim();
        if (search.Length > 100) search = search[..100];
        var now = DateTime.UtcNow;
        var query = context.MarketAuctions.AsNoTracking().AsQueryable();
        if (mode == "history") query = query.Where(x => x.SellerId == UserId && x.Status != AuctionStatus.Active);
        else
        {
            query = query.Where(x => x.Status == AuctionStatus.Active);
            query = mode == "sales" ? query.Where(x => x.SellerId == UserId) : query.Where(x => x.EndsAt > now);
        }
        if (search.Length > 0) { var lower = search.ToLower(); query = query.Where(x => x.Card.Name.ToLower().Contains(lower)); }
        var total = await query.CountAsync();
        var rows = await query.Include(x => x.Seller).Include(x => x.HighestBidder)
            .OrderByDescending(x => mode == "history" ? x.ClosedAt : null).ThenBy(x => x.EndsAt)
            .Skip((page-1)*20).Take(20).Select(x => new { Auction=x, Count=x.Bids.Count }).ToListAsync();
        var cards = (await presentation.GetAsync(rows.Select(x => x.Auction.CardId))).ToDictionary(x => x.Id);
        return new MarketIndexViewModel
        {
            Mode=mode, Page=page, Search=search, Total=total,
            ActiveSales=await context.MarketAuctions.CountAsync(x => x.SellerId == UserId && x.Status == AuctionStatus.Active),
            Listings=rows.Select(x => new MarketListingViewModel { Auction=x.Auction, Card=cards[x.Auction.CardId],
                IsSeller=x.Auction.SellerId == UserId, IsHighestBidder=x.Auction.HighestBidderId == UserId, BidCount=x.Count }).ToList()
        };
    }

    [HttpGet("Auction/{id:guid}")]
    public async Task<IActionResult> Auction(Guid id)
    {
        var model = await DetailAsync(id);
        return model == null ? NotFound() : View(model);
    }
    [HttpGet("Auction/{id:guid}/State")]
    public async Task<IActionResult> State(Guid id)
    {
        var model = await DetailAsync(id);
        return model == null ? NotFound() : PartialView("_AuctionState",model);
    }
    private async Task<MarketDetailViewModel?> DetailAsync(Guid id)
    {
        var auction = await context.MarketAuctions.AsNoTracking().Include(x => x.Seller).Include(x => x.HighestBidder).FirstOrDefaultAsync(x => x.Id == id);
        if (auction == null) return null;
        var card = (await presentation.GetAsync(new[] {auction.CardId})).Single();
        return new MarketDetailViewModel
        {
            Listing=new MarketListingViewModel { Auction=auction, Card=card, IsSeller=auction.SellerId == UserId,
                IsHighestBidder=auction.HighestBidderId == UserId, BidCount=await context.MarketBids.CountAsync(x => x.AuctionId == id) },
            AvailableBalance=await context.Users.Where(x => x.Id == UserId).Select(x => x.CardBalance).SingleAsync(),
            Bids=await context.MarketBids.AsNoTracking().Where(x => x.AuctionId == id).Include(x => x.Bidder)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(30).ToListAsync()
        };
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(string search = "", int page = 1) => View(await CreateModelAsync(new(),search,page));
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAuctionInput input)
    {
        if (ModelState.IsValid)
        {
            try { return RedirectToAction(nameof(Auction), new { id=input.DurationMinutes.HasValue
                ? await market.CreateForMinutesAsync(UserId,input.CardId,input.StartingPrice,input.DurationMinutes.Value)
                : await market.CreateAsync(UserId,input.CardId,input.StartingPrice,input.DurationHours) }); }
            catch (MarketRuleException ex) { ModelState.AddModelError("", ex.Message); }
        }
        return View(await CreateModelAsync(input,"",1));
    }
    private async Task<CreateAuctionViewModel> CreateModelAsync(CreateAuctionInput input, string search, int page)
    {
        page=Math.Clamp(page,1,100000); search=(search ?? "").Trim();
        if (search.Length > 100) search=search[..100];
        var query=context.UserCardCollections.AsNoTracking().Where(x => x.UserId == UserId && x.Quantity > 0);
        if (search.Length > 0) { var lower=search.ToLower(); query=query.Where(x => x.Card.Name.ToLower().Contains(lower)); }
        var owned=await query.OrderBy(x => x.Card.Name).ThenBy(x => x.CardId).Skip((page-1)*40).Take(41)
            .Select(x => new {x.CardId,x.Quantity}).ToListAsync();
        var cards=(await presentation.GetAsync(owned.Take(40).Select(x => x.CardId))).ToDictionary(x => x.Id);
        return new CreateAuctionViewModel { Input=input, Search=search, Page=page, HasNext=owned.Count>40,
            ActiveSales=await context.MarketAuctions.CountAsync(x => x.SellerId == UserId && x.Status == AuctionStatus.Active),
            Cards=owned.Take(40).Select(x => (cards[x.CardId],x.Quantity)).ToList() };
    }

    [HttpPost("Auction/{id:guid}/Bid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bid(Guid id, [FromBody] MarketBidInput input)
    {
        if (!ModelState.IsValid) return BadRequest(new {message="Montant invalide."});
        try { await market.BidAsync(id,UserId,input.Amount); return Ok(new {message="Votre enchère est enregistrée. Son montant est réservé jusqu’à la fin de la vente ou une surenchère."}); }
        catch (MarketRuleException ex) { return BadRequest(new {message=ex.Message}); }
    }
    [HttpPost("Auction/{id:guid}/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try { await market.CancelAsync(id,UserId); return Ok(new {message="Vente annulée. La carte est revenue dans votre inventaire."}); }
        catch (MarketRuleException ex) { return BadRequest(new {message=ex.Message}); }
    }
}
