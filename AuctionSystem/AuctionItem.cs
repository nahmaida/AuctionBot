namespace AuctionSystem
{
    public class AuctionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageId { get; set; }
        public decimal InitialPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public UserAccount Creator {  get; set; }
        public UserAccount HighestBidder { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Transaction> BidHistory { get; set; }

        public AuctionItem(string name, string description, string imageId, decimal initialPrice, UserAccount creator, TimeSpan duration)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            ImageId = imageId;
            InitialPrice = initialPrice;
            CurrentPrice = initialPrice;
            Creator = creator;
            HighestBidder = Creator;
            IsActive = true;
            CreatedAt = DateTime.Now;
            EndTime = CreatedAt.Add(duration);
            BidHistory = new List<Transaction>();
        }

        public string GetCaption()
        {
            return $"<b>Имя:</b> {Name}\n\n" +
                  $"Описание: {Description}\n\n" +
                  $"💰 <b>Наибольшая ставка:</b> {HighestBidder.Username}: {CurrentPrice}\n" +
                  $"👤 <b>Создатель:</b> {Creator.Username}\n" +
                  $"⏰ <b>Заканичвается:</b> {EndTime:yyyy-MM-dd HH:mm}";
        }
    }
}
