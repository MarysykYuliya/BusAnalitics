using System.Text;
using System.Text.Json;
using BusinessAnalytics.Services.Interfaces;

namespace BusinessAnalytics.Services.Realization
{
    public class AiService : IAiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public AiService(IConfiguration configuration, HttpClient httpClient)
        {
            _apiKey = configuration["GeminiApiKey"] ?? throw new ArgumentNullException("Gemini API Key is missing!");
            _httpClient = httpClient;
        }

        public async Task<string> GenerateFinancialReportAsync(string businessName, decimal income, decimal expense, int transactionCount, string topCategory)
        {
            string prompt = $@"
                Ти професійний фінансовий директор (CFO). Проаналізуй малий бізнес '{businessName}'.
                Дані за вибраний період:
                - Дохід: {income} грн
                - Витрати: {expense} грн
                - Кількість транзакцій: {transactionCount}
                - Найбільш прибуткова категорія: {topCategory}
                
                Напиши чіткий бізнес-звіт українською мовою. 
                Він має містити: 1) Оцінку прибутковості (чи все добре). 2) Головний ризик або на що звернути увагу. 3) Дві конкретні поради для збільшення прибутку.
                Використовуй HTML-теги для форматування: <b> для жирного тексту, <ul> та <li> для списків, <br> для нових рядків. Не використовуй Markdown (**).
            ";

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    return $"<b>Помилка від Google (Код: {response.StatusCode}):</b><br><small>{errorDetails}</small>";
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                var textResult = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                return textResult ?? "Звіт порожній.";
            }
            catch (Exception)
            {
                return "<b>Помилка:</b> Виникла проблема під час генерації звіту.";
            }
        }
    }
}