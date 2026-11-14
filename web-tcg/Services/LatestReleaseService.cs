using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using web_tcg.Infrastructure;

namespace web_tcg.Services;

public sealed record AppReleaseInfo(string Version, string? Url);

/// <summary>Published releases, including prereleases, independently of the image's Git metadata.</summary>
public sealed class LatestReleaseService(IHttpClientFactory clients, IConfiguration configuration,
    TimeProvider clock, ILogger<LatestReleaseService> logger)
{
    public const string ClientName = "GitHubReleases";
    private readonly SemaphoreSlim gate = new(1,1);
    private DateTimeOffset nextRefresh = DateTimeOffset.MinValue;
    private AppReleaseInfo? lastKnown;

    public async Task<AppReleaseInfo> GetAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (clock.GetUtcNow() >= nextRefresh)
            {
                nextRefresh = clock.GetUtcNow().AddMinutes(5);
                var repository = configuration["GitHub:Repository"] ?? "SaitamTheBest/Worlds-TCG";
                if (!Regex.IsMatch(repository,@"\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z"))
                    return lastKnown ?? new(AppVersion.Current,null);
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{repository}/releases?per_page=100");
                    // Optional read-only token for private repositories; never sent to the browser.
                    var token = configuration["GitHub:Token"];
                    if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
                    using var response = await clients.CreateClient(ClientName).SendAsync(request,ct);
                    response.EnsureSuccessStatusCode();
                    using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
                    var published = json.RootElement.EnumerateArray().Where(release =>
                        release.TryGetProperty("draft",out var draft) && draft.ValueKind == JsonValueKind.False &&
                        release.TryGetProperty("tag_name",out var tag) && tag.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.GetString()) &&
                        release.TryGetProperty("published_at",out var date) && date.ValueKind == JsonValueKind.String && date.TryGetDateTimeOffset(out _))
                        .OrderByDescending(release => release.GetProperty("published_at").GetDateTimeOffset()).FirstOrDefault();
                    if (published.ValueKind != JsonValueKind.Undefined)
                    {
                        var tag = published.GetProperty("tag_name").GetString()!.Trim();
                        lastKnown = new(tag.StartsWith('v') || tag.StartsWith('V') ? tag : $"v{tag}",
                            $"https://github.com/{repository}/releases/tag/{Uri.EscapeDataString(tag)}");
                    }
                    else lastKnown = null;
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
                {
                    logger.LogWarning("Dernière release GitHub indisponible ({ErrorType}) ; version précédente conservée.",ex.GetType().Name);
                }
            }
            return lastKnown ?? new(AppVersion.Current,null);
        }
        finally { gate.Release(); }
    }
}
