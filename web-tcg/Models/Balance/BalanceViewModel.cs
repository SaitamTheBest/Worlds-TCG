namespace web_tcg.Models.Balance
{
    public class BalanceViewModel
    {
        public decimal Balance { get; set; }

        public DateTime? LastBalanceRewardAt { get; set; }

        public DateTime? NextRewardAt =>
            LastBalanceRewardAt?.AddMinutes(10);
    }
}
