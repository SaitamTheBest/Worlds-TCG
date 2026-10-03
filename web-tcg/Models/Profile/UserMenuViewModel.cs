namespace web_tcg.Models.Profile
{
    public sealed class UserMenuViewModel
    {
        public string Username { get; init; } = string.Empty;
        public string? AvatarUrl { get; init; }
        public bool IsAdmin { get; init; }
    }
}