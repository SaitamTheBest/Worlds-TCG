using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using web_tcg.Data.Entities;
using web_tcg.Services;

internal static class PasswordRecoveryChecks
{
    public static async Task RunAsync(Action<bool,string> check)
    {
        var store = new MemoryUsers();
        var email = new CaptureEmail();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>(o => { o.Password.RequiredLength=6; o.Password.RequireDigit=false; o.Password.RequireLowercase=false; o.Password.RequireUppercase=false; o.Password.RequireNonAlphanumeric=false; })
            .AddDefaultTokenProviders();
        services.AddSingleton<IUserStore<ApplicationUser>>(store);
        using var provider = services.BuildServiceProvider();
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var person = new ApplicationUser { UserName="alice", Email="alice@example.test" };
        check((await users.CreateAsync(person,"before123")).Succeeded,"création du compte de test de réinitialisation");
        var recovery = new PasswordRecoveryService(users,email,Options.Create(new PublicSiteOptions { BaseUrl="https://worlds-tcg.com" }));
        await recovery.RequestAsync("absent@example.test");
        check(email.Url == null,"aucun mail envoyé pour un compte absent");
        await recovery.RequestAsync(person.Email);
        var url = new Uri(email.Url!);
        var token = QueryHelpers.ParseQuery(url.Query)["token"].ToString();
        check(url.Host == "worlds-tcg.com" && url.Scheme == "https","lien envoyé sur le domaine configuré");
        check(!(await recovery.ResetAsync(person.Id,"broken-token","after123")).Succeeded,"jeton invalide refusé");
        check((await recovery.ResetAsync(person.Id,token,"after123")).Succeeded,"réinitialisation avec un vrai jeton Identity");
        check(await users.CheckPasswordAsync(person,"after123") && !await users.CheckPasswordAsync(person,"before123"),"nouveau mot de passe accepté et ancien refusé");
        check(!(await recovery.ResetAsync(person.Id,token,"another123")).Succeeded,"lien déjà utilisé refusé");
        await recovery.RequestAsync(person.Email);
        var expired = QueryHelpers.ParseQuery(new Uri(email.Url!).Query)["token"].ToString();
        provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan = TimeSpan.FromMinutes(-1);
        check(!(await recovery.ResetAsync(person.Id,expired,"another123")).Succeeded,"lien expiré refusé");
        email.Url=null; person.IsBot=true;
        await recovery.RequestAsync(person.Email);
        check(email.Url==null,"aucune réinitialisation du compte bot");
    }
    private sealed class CaptureEmail : ITransactionalEmailSender
    {
        public string? Url;
        public Task SendPasswordResetAsync(string recipient,string resetUrl,CancellationToken ct=default) { Url=resetUrl; return Task.CompletedTask; }
    }
    private sealed class MemoryUsers : IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>
    {
        private readonly List<ApplicationUser> records=[];
        public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.LockoutEnd);
        public Task SetLockoutEndDateAsync(ApplicationUser u,DateTimeOffset? n,CancellationToken c) {u.LockoutEnd=n;return Task.CompletedTask;}
        public Task<int> IncrementAccessFailedCountAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(++u.AccessFailedCount);
        public Task ResetAccessFailedCountAsync(ApplicationUser u,CancellationToken c) {u.AccessFailedCount=0;return Task.CompletedTask;}
        public Task<int> GetAccessFailedCountAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.AccessFailedCount);
        public Task<bool> GetLockoutEnabledAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.LockoutEnabled);
        public Task SetLockoutEnabledAsync(ApplicationUser u,bool n,CancellationToken c) {u.LockoutEnabled=n;return Task.CompletedTask;}
        public void Dispose() { }
        public Task<IdentityResult> CreateAsync(ApplicationUser u,CancellationToken c) {records.Add(u);return Task.FromResult(IdentityResult.Success);}
        public Task<IdentityResult> UpdateAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser u,CancellationToken c) {records.Remove(u);return Task.FromResult(IdentityResult.Success);}
        public Task<ApplicationUser?> FindByIdAsync(string id,CancellationToken c)=>Task.FromResult(records.FirstOrDefault(u=>u.Id==id));
        public Task<ApplicationUser?> FindByNameAsync(string n,CancellationToken c)=>Task.FromResult(records.FirstOrDefault(u=>u.NormalizedUserName==n));
        public Task<ApplicationUser?> FindByEmailAsync(string n,CancellationToken c)=>Task.FromResult(records.FirstOrDefault(u=>u.NormalizedEmail==n));
        public Task<string> GetUserIdAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.UserName);
        public Task SetUserNameAsync(ApplicationUser u,string? n,CancellationToken c) {u.UserName=n;return Task.CompletedTask;}
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser u,string? n,CancellationToken c) {u.NormalizedUserName=n;return Task.CompletedTask;}
        public Task<string?> GetEmailAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.Email);
        public Task SetEmailAsync(ApplicationUser u,string? n,CancellationToken c) {u.Email=n;return Task.CompletedTask;}
        public Task<string?> GetNormalizedEmailAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.NormalizedEmail);
        public Task SetNormalizedEmailAsync(ApplicationUser u,string? n,CancellationToken c) {u.NormalizedEmail=n;return Task.CompletedTask;}
        public Task<bool> GetEmailConfirmedAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.EmailConfirmed);
        public Task SetEmailConfirmedAsync(ApplicationUser u,bool n,CancellationToken c) {u.EmailConfirmed=n;return Task.CompletedTask;}
        public Task<string?> GetPasswordHashAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.PasswordHash);
        public Task<bool> HasPasswordAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.PasswordHash!=null);
        public Task SetPasswordHashAsync(ApplicationUser u,string? n,CancellationToken c) {u.PasswordHash=n;return Task.CompletedTask;}
        public Task<string?> GetSecurityStampAsync(ApplicationUser u,CancellationToken c)=>Task.FromResult(u.SecurityStamp);
        public Task SetSecurityStampAsync(ApplicationUser u,string n,CancellationToken c) {u.SecurityStamp=n;return Task.CompletedTask;}
    }
}
