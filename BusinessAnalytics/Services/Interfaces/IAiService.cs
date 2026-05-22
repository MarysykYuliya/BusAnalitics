namespace BusinessAnalytics.Services.Interfaces
{
    public interface IAiService
    {
        Task<string> GenerateFinancialReportAsync(string businessName, decimal income, decimal expense, int transactionCount, string topCategory);
    }
}