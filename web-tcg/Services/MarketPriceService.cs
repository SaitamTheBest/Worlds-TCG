using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Models.Market;

namespace web_tcg.Services;

public sealed class MarketPriceService(ApplicationDbContext context)
{
    public async Task<MarketPriceViewModel> GetAsync(int cardId)
    {
        // Une variante est une carte physique distincte : ne pas mélanger ses ventes à celles de la carte normale.
        var sold = context.MarketAuctions.AsNoTracking()
            .Where(x => x.CardId == cardId && x.Status == AuctionStatus.Sold && x.ClosedAt != null);
        var summary = await sold.GroupBy(x => x.CardId)
            .Select(g => new { Average = g.Average(x => x.CurrentPrice), Count = g.Count() }).SingleOrDefaultAsync();
        var since = DateTime.UtcNow.Date.AddDays(-89);
        var points = await sold.Where(x => x.ClosedAt >= since).GroupBy(x => x.ClosedAt!.Value.Date)
            .OrderBy(g => g.Key).Select(g => new MarketPricePoint {
                Date = g.Key, AveragePrice = g.Average(x => x.CurrentPrice), SaleCount = g.Count()
            }).ToListAsync();
        return new MarketPriceViewModel { CardId = cardId, AveragePrice = summary?.Average,
            SaleCount = summary?.Count ?? 0, Points = points };
    }
}
