using web_tcg.Data.Entities;

namespace web_tcg.Services;

public static class BoosterAvailability
{
    public static IQueryable<CardSet> AvailableForBoosters(this IQueryable<CardSet> sets, DateTime now)
        => sets.Where(set => set.IsActive && set.ReleaseDate <= now);
}
