namespace AuctionSystem.Domain
{
    /// <summary>
    /// Аккаунт ползователя с id, именем, балансом
    /// </summary>
    public class UserAccount
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public decimal Balance { get; set; }

        public UserAccount(long id, string username)
        {
            Id = id;
            Username = username;
            // Начальный баланс - 10000 рублей
            Balance = 10000m;
        }
    }
}