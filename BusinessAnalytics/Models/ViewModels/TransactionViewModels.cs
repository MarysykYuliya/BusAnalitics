using BusinessAnalytics.Models.Entities;

namespace BusinessAnalytics.Models.ViewModels
{
    public class TransactionGroupCreateDTO
    {
        public Guid BusinessId { get; set; }
        public TransactionType TransactionType { get; set; }
        public DateTime Date { get; set; } = DateTime.Today;

        public List<TransactionItemDTO> Items { get; set; } = new();
    }

    public class TransactionItemDTO
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
