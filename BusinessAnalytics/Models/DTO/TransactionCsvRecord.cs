namespace BusinessAnalytics.Models.DTO
{
    public class TransactionCsvRecord
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; } 
        public string Description { get; set; }
    }
}