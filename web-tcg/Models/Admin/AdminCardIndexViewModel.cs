using Microsoft.AspNetCore.Mvc.Rendering;

namespace web_tcg.Models.Admin;

public sealed class AdminCardIndexViewModel
{
    public List<AdminCardListItemViewModel> Items { get; init; } = new();
    public List<SelectListItem> CardSets { get; init; } = new();
    public int? CardSetId { get; init; }
    public string Rarity { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public int Total { get; init; }
    public int BaseCardCount { get; init; }
    public int VariantCount => Total - BaseCardCount;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
