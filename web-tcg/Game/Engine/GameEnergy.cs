using web_tcg.Game.Runtime;

namespace web_tcg.Game.Engine;

public static class GameEnergy
{
    public const int Maximum = 750;

    public static int Gain(GamePlayerState player, int amount)
    {
        player.Money = Math.Clamp(player.Money, 0, Maximum);
        var gained = Math.Min(Math.Max(0, amount), Maximum - player.Money);
        player.Money += gained;
        return gained;
    }
}
