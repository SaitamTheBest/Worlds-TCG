using System.Text.Json;
using web_tcg.Models.Admin;

public static class CardPreviewChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Same JSON names and serializer options as the create form and MVC.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var partyId in new int?[] { null, 42 })
        {
            var json = $$"""{"name":"Carte","countryName":"France","politicalPartyId":{{(partyId?.ToString() ?? "null")}},"abilities":[]}""";
            var preview = JsonSerializer.Deserialize<CardPreviewViewModel>(json, options)!;
            check(preview.CountryName == "France" && preview.PoliticalPartyId == partyId,
                $"aperçu : pays et parti distincts dans le JSON (parti {partyId?.ToString() ?? "absent"})");
            using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(preview, options));
            check(serialized.RootElement.GetProperty("countryName").GetString() == "France"
                && serialized.RootElement.GetProperty("politicalPartyId").ToString() == (partyId?.ToString() ?? ""),
                "aperçu : sérialisation sans collision entre pays et parti politique");
        }
    }
}
