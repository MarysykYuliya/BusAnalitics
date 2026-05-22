namespace BusinessAnalytics.Models.Configurations
{
    public class TelegramSettings
    {
        public string BotToken { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;
        public int ReportHour { get; set; } = 20;
        public string BotUsername { get; set; } = string.Empty; // e.g. "MyBusinessBot" (without @)
    }
}
