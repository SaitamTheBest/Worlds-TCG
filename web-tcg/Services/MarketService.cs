using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Hubs;

namespace web_tcg.Services;

public sealed class MarketRuleException(string message) : Exception(message);

public sealed class MarketService(ApplicationDbContext context, IHubContext<MarketHub> hub,
    CardBalanceRealtimeService balances, ILogger<MarketService> logger)
{
    public const int MaxActiveSales = 10;
    public const decimal MaxPrice = 999999999m;

    public async Task<Guid> CreateAsync(string userId, int cardId, decimal price, int hours)
    {
        if (price < 1 || price > MaxPrice || decimal.Round(price,2) != price)
            throw new MarketRuleException("Le prix doit être compris entre 1 et 999 999 999, avec deux décimales maximum.");
        if (hours is not (1 or 6 or 24 or 48)) throw new MarketRuleException("Choisissez une durée de 1 h, 6 h, 24 h ou 48 h.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        await CollectionTransactionLock.AcquireAsync(context);
        var seller = await context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId && !x.IsBot);
        if (seller == null) throw new MarketRuleException("Compte vendeur introuvable.");
        if (await context.MarketAuctions.CountAsync(x => x.SellerId == userId && x.Status == AuctionStatus.Active) >= MaxActiveSales)
            throw new MarketRuleException("Vous avez déjà 10 ventes en cours. Attendez leur clôture ou annulez une vente sans enchère.");
        var owned = await context.UserCardCollections.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId && x.Quantity > 0);
        if (owned == null) throw new MarketRuleException("Vous ne possédez plus d’exemplaire disponible de cette carte.");
        owned.Quantity--;
        if (owned.Quantity == 0)
        {
            context.UserCardCollections.Remove(owned);
            await context.UserProfileShowcaseCards.Where(x => x.UserId == userId && x.CardId == cardId).ExecuteDeleteAsync();
        }
        var auction = new MarketAuction { SellerId=userId, CardId=cardId, StartingPrice=price, CurrentPrice=price, EndsAt=DateTime.UtcNow.AddHours(hours) };
        context.MarketAuctions.Add(auction);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        await PublishAsync(auction.Id);
        return auction.Id;
    }

    public async Task BidAsync(Guid id, string userId, decimal amount)
    {
        if (amount < 1 || amount > MaxPrice || decimal.Round(amount,2) != amount)
            throw new MarketRuleException("Le montant doit être compris entre 1 et 999 999 999, avec deux décimales maximum.");
        await using var transaction = await context.Database.BeginTransactionAsync();
        await CollectionTransactionLock.AcquireAsync(context);
        var auction = await context.MarketAuctions.FirstOrDefaultAsync(x => x.Id == id);
        if (auction == null) throw new MarketRuleException("Cette vente est introuvable.");
        if (auction.Status != AuctionStatus.Active || auction.EndsAt <= DateTime.UtcNow)
            throw new MarketRuleException("Cette enchère est terminée.");
        if (auction.SellerId == userId) throw new MarketRuleException("Vous ne pouvez pas enchérir sur votre propre vente.");
        var minimum = auction.HighestBidderId == null ? auction.StartingPrice : auction.CurrentPrice + 1;
        if (amount < minimum) throw new MarketRuleException($"Une autre enchère vous précède. Le montant minimum est maintenant de {minimum:0.##} ¤.");
        var previousBidder = auction.HighestBidderId;
        var debit = previousBidder == userId ? amount - auction.CurrentPrice : amount;
        var debited = await context.Users.Where(x => x.Id == userId && !x.IsBot && x.CardBalance >= debit)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CardBalance, x => x.CardBalance - debit));
        if (debited != 1) throw new MarketRuleException("Votre solde marché disponible ne suffit pas pour cette enchère.");
        if (previousBidder != null && previousBidder != userId)
            await context.Users.Where(x => x.Id == previousBidder).ExecuteUpdateAsync(s => s.SetProperty(x => x.CardBalance, x => x.CardBalance + auction.CurrentPrice));
        auction.HighestBidderId = userId;
        auction.CurrentPrice = amount;
        context.MarketBids.Add(new MarketBid { AuctionId=id, BidderId=userId, Amount=amount });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        await PublishAsync(id, previousBidder, userId);
    }

    public async Task CancelAsync(Guid id, string userId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await CollectionTransactionLock.AcquireAsync(context);
        var auction = await context.MarketAuctions.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (auction == null) throw new MarketRuleException("Cette vente ne vous appartient pas.");
        if (auction.Status != AuctionStatus.Active || auction.EndsAt <= DateTime.UtcNow || auction.HighestBidderId != null)
            throw new MarketRuleException("Seule une vente en cours sans enchère peut être annulée.");
        await ReturnCardAsync(auction.SellerId, auction.CardId);
        auction.Status = AuctionStatus.Cancelled;
        auction.ClosedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        await PublishAsync(id);
    }

    public async Task CloseExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ids = await context.MarketAuctions.AsNoTracking().Where(x => x.Status == AuctionStatus.Active && x.EndsAt <= now)
            .OrderBy(x => x.EndsAt).Select(x => x.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await CollectionTransactionLock.AcquireAsync(context, cancellationToken);
            var auction = await context.MarketAuctions.FirstAsync(x => x.Id == id, cancellationToken);
            if (auction.Status != AuctionStatus.Active || auction.EndsAt > DateTime.UtcNow) continue;
            if (auction.HighestBidderId != null)
            {
                await ReturnCardAsync(auction.HighestBidderId, auction.CardId);
                await context.Users.Where(x => x.Id == auction.SellerId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CardBalance, x => x.CardBalance + auction.CurrentPrice), cancellationToken);
                auction.Status = AuctionStatus.Sold;
            }
            else
            {
                await ReturnCardAsync(auction.SellerId, auction.CardId);
                auction.Status = AuctionStatus.Unsold;
            }
            auction.ClosedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await PublishAsync(id, auction.SellerId, auction.HighestBidderId);
        }
    }

    private async Task ReturnCardAsync(string userId, int cardId)
    {
        var owned = await context.UserCardCollections.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
        if (owned == null) context.UserCardCollections.Add(new UserCardCollection { UserId=userId, CardId=cardId, Quantity=1 });
        else owned.Quantity++;
    }

    private async Task PublishAsync(Guid id, params string?[] userIds)
    {
        try
        {
            await hub.Clients.All.SendAsync("MarketChanged", new { auctionId=id });
            foreach (var userId in userIds.Where(x => x != null).Distinct())
            {
                var balance = await context.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.CardBalance).SingleAsync();
                await balances.PublishAsync(userId!, balance);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Marché enregistré mais notification SignalR indisponible pour {AuctionId}", id); }
    }
}
