using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using web_tcg.Infrastructure;
using web_tcg.Services;

public static class LatestReleaseChecks
{
    sealed class Clock : TimeProvider {
        public DateTimeOffset Now = new(2026,11,14,12,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    sealed class Handler : HttpMessageHandler {
        public string Json = "[]";
        public HttpStatusCode Status = HttpStatusCode.OK;
        public int Calls;
        public string? Url, Token;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            Calls++;Url=request.RequestUri!.AbsoluteUri;Token=request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(Status) {Content=new StringContent(Json,Encoding.UTF8,"application/json")});
        }
    }
    sealed class Factory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    public static async Task RunAsync(Action<bool,string> check)
    {
        var handler=new Handler();using var client=new HttpClient(handler);var clock=new Clock();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["GitHub:Repository"]="SaitamTheBest/Worlds-TCG",["GitHub:Token"]="test-read-only-token" }).Build();
        LatestReleaseService Service() => new(new Factory(client),config,clock,NullLogger<LatestReleaseService>.Instance);
        var service=Service();
        handler.Json="""
        [
            {"tag_name":"v9.0.0","draft":true,"published_at":"2026-11-14T12:00:00Z"},
            {"tag_name":"v0.9.0","draft":false,"prerelease":false,"published_at":"2026-11-01T12:00:00Z"},
            {"tag_name":"v1.0.0-alpha","draft":false,"prerelease":true,"published_at":"2026-11-12T12:00:00Z"}
        ]
        """;
        var release=await service.GetAsync();
        check(release.Version=="v1.0.0-alpha"&&release.Url=="https://github.com/SaitamTheBest/Worlds-TCG/releases/tag/v1.0.0-alpha",
            "footer : dernière publication sélectionnée, préversions incluses et brouillons exclus");
        check(handler.Url!.EndsWith("/releases?per_page=100")&&handler.Token=="test-read-only-token",
            "footer : API des releases utilisée et jeton privé limité à la requête serveur");
        await Task.WhenAll(Enumerable.Range(0,10).Select(_=>service.GetAsync()));
        check(handler.Calls==1,"footer : requête GitHub partagée entre visiteurs et mise en cache cinq minutes");
        clock.Now=clock.Now.AddMinutes(6);handler.Json="""[{"tag_name":"1.0.1","draft":false,"published_at":"2026-11-14T12:00:00Z"}]""";
        check((await service.GetAsync()).Version=="v1.0.1"&&handler.Calls==2,"footer : nouvelle release détectée sans redémarrage et préfixe normalisé");
        clock.Now=clock.Now.AddMinutes(6);handler.Status=HttpStatusCode.Forbidden;
        check((await service.GetAsync()).Version=="v1.0.1","footer : panne ou limite GitHub conserve la dernière release connue");
        await service.GetAsync();check(handler.Calls==3,"footer : erreur GitHub mise en cache sans répétition des requêtes");
        var offline=await Service().GetAsync();
        check(offline.Version==AppVersion.Current&&offline.Url==null,"footer : première connexion impossible utilise la version locale sans casser la page");
        handler.Status=HttpStatusCode.OK;handler.Json="invalid-json";
        check((await Service().GetAsync()).Url==null,"footer : réponse invalide gérée sans erreur dans la vue");
        handler.Json="[]";check((await Service().GetAsync()).Url==null,"footer : dépôt sans release conserve le repli local");
        config["GitHub:Repository"]="https://malicious.example/repo";var before=handler.Calls;
        check((await Service().GetAsync()).Url==null&&handler.Calls==before,"footer : configuration de dépôt invalide ne provoque aucune requête externe");
    }
}
