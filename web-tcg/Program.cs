using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;
using web_tcg.Game;
using web_tcg.Game.Effects;
using web_tcg.Game.Engine;
using web_tcg.Hubs;
using web_tcg.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "WorldsTCG.Auth";

    options.LoginPath = "/Auth";
    options.AccessDeniedPath = "/Auth";

    // Le cookie persistant "Se souvenir de moi"
    // sera valable pendant 60 jours.
    options.ExpireTimeSpan = TimeSpan.FromDays(60);

    // À chaque utilisation du site, la durée du cookie
    // est renouvelée si nécessaire.
    options.SlidingExpiration = true;

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // En production ton site devra être en HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddSignalR();

builder.Services.AddScoped<BoosterRarityGenerator>();
builder.Services.AddScoped<BoosterGenerator>();
builder.Services.AddScoped<BoosterCardSelectionService>();
builder.Services.AddScoped<CardCollectionService>();
builder.Services.AddScoped<BoosterCardSelectionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<CardBalanceRealtimeService>();

builder.Services.AddHostedService<BalanceRewardService>();

// Moteur de jeu
builder.Services.Configure<GameRewardOptions>(
    builder.Configuration.GetSection("GameRewards"));

builder.Services.AddScoped<DeckValidationService>();
builder.Services.AddScoped<CardPresentationService>();
builder.Services.AddScoped<BotDeckSetupService>();
builder.Services.AddScoped<GameStateFactory>();
builder.Services.AddScoped<GameStateRepository>();
builder.Services.AddScoped<GameStateProjector>();
builder.Services.AddScoped<GameAiService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<GameMatchLockProvider>();
builder.Services.AddSingleton<MatchmakingService>();

builder.Services.AddScoped<GameTargetResolver>();
builder.Services.AddScoped<GameConditionEvaluator>();
builder.Services.AddScoped<GameEffectEngine>();
builder.Services.AddScoped<GameEngine>();

builder.Services.AddScoped<IGameEffectHandler, DamageEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, HealEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, DrawCardsEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, DiscardCardsEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, GainMoneyEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, LoseMoneyEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, StealMoneyEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, BlockEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, ShieldEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, StealthEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, ExtraAttackEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, DamageMultiplierEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, HealthBonusEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, DamageBonusEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, ReviveEffectHandler>();
builder.Services.AddScoped<IGameEffectHandler, DestroyLocationEffectHandler>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        dbContext.Database.Migrate();

        // Seed le rôle Admin s'il n'existe pas
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            logger.LogInformation("Rôle Admin créé avec succès.");
        }

        // Seed du compte bot et de son deck, utilisés pour
        // les parties contre l'IA.
        var userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        var botUser =
            await userManager.FindByNameAsync("AI_BOT");

        if (botUser == null)
        {
            botUser =
                new ApplicationUser
                {
                    UserName = "AI_BOT",
                    Email = "ai-bot@worlds-tcg.local",
                    EmailConfirmed = true,
                    IsBot = true
                };

            var createResult =
                await userManager.CreateAsync(
                    botUser,
                    Guid.NewGuid().ToString("N") + "Aa1!");

            if (createResult.Succeeded)
            {
                logger.LogInformation(
                    "Compte bot IA créé avec succès.");
            }
            else
            {
                logger.LogError(
                    "Impossible de créer le compte bot IA : {Errors}",
                    string.Join(
                        ", ",
                        createResult.Errors.Select(x => x.Description)));
                botUser = null;
            }
        }

        if (botUser != null && !string.IsNullOrEmpty(botUser.Id))
            await services.GetRequiredService<BotDeckSetupService>().EnsureAsync(botUser.Id);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Une erreur est survenue lors de l'initialisation de la base de données.");

        throw;
    }
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Middleware pour rediriger les utilisateurs non authentifiés
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // WHITELIST des routes publiques (ne pas rediriger)
    // Ces routes sont accessibles sans authentification
    var publicRoutes = new[] 
    { 
        "/auth",                  // Page d'authentification
        "/lib",                   // Librairies
        "/css",                   // Styles
        "/js",                    // Scripts
        "/images",                // Images
        "/favicon",               // Favicon
        "/error",                 // Page d'erreur
        "/",                      // Racine (Home)
        "/home",                  // Home page
    };

    bool isPublicRoute = publicRoutes.Any(route => 
        path == route || path.StartsWith(route.TrimEnd('/') + "/")
    );

    // Si c'est une route publique, laisser passer directement
    if (isPublicRoute)
    {
        await next();
        return;
    }

    // Si l'utilisateur n'est pas authentifié ET tente d'accéder à une route protégée
    // Le système d'Identity se chargera de la redirection vers LoginPath (/Auth)
    // Ce middleware est juste un filet de sécurité supplémentaire
    if (context.User.Identity?.IsAuthenticated != true)
    {
        // Laisser Identity gérer la redirection via l'attribut [Authorize]
        // ou notre middleware personnalisé
        logger.LogWarning($"❌ Tentative d'accès non authentifiée à : {path}");
        context.Response.Redirect("/Auth");
        return;
    }

    // Utilisateur authentifié, continuer
    await next();
});

app.UseStaticFiles();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<web_tcg.Hubs.BalanceHub>("/hubs/balance");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<CardBalanceHub>("/hubs/card-balance");
app.MapHub<GameHub>("/hubs/game");
app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();
