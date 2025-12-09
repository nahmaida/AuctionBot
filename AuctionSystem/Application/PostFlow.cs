namespace AuctionSystem.Application
{
    /// <summary>
    /// Helper для временного хранения данных при создании лота
    /// </summary>
    public class PostFlow
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageId { get; set; }
        public decimal? Price { get; set; }
        public double? Duration { get; set; }
    }

}
