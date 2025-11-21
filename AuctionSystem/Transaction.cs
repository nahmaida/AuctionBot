namespace AuctionSystem
{
    /// <summary>
    /// Транзакция с получателем, суммой, временем. Отвечает за перевод денег
    /// </summary>
    public class Transaction
    {
        public UserAccount User { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }

        public Transaction(UserAccount user, decimal amount, DateTime timestamp)
        {
            User = user;
            Amount = amount;
            Timestamp = timestamp;
        }

        public bool TryProcess()
        {
            // Если мы пытаемся СПИСАТЬ деньги (Amount отрицательный),
            // проверяем, чтобы баланс не стал меньше нуля
            if (Amount < 0 && (User.Balance + Amount < 0))
            {
                return false;
            }

            User.Balance += Amount;
            return true;
        }
    }
}