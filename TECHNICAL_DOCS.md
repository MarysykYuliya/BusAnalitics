# 📊 BusinessAnalytics — Технічна документація

> ASP.NET Core 8 MVC веб-застосунок для аналітики малого бізнесу з підтримкою кількох бізнесів, фінансовими звітами, AI-консультантом та Telegram-сповіщеннями.

---

## 🏗️ Загальна архітектура

```
BusinessAnalytics/
├── Controllers/         # MVC-контролери (обробка HTTP-запитів)
├── Services/
│   ├── Interfaces/      # Контракти сервісів (DI abstractions)
│   └── Realization/     # Реалізації сервісів + фонові служби
├── Models/
│   ├── Entities/        # EF Core-сутності (таблиці БД)
│   ├── DTO/             # Data Transfer Objects (між сервісами і view)
│   ├── ViewModels/      # Моделі для Razor Views
│   ├── Interfaces/      # IBusinessMetric (патерн Strategy)
│   ├── Metrics/         # Реалізації метрик (Profit, Revenue, Expenses)
│   └── Configurations/  # SMTP / Telegram налаштування (IOptions)
├── Data/
│   ├── ApplicationDbContext.cs   # EF Core DbContext
│   └── Seed/                     # Ініціалізація ролей
├── Views/               # Razor Views (.cshtml)
├── wwwroot/             # Статичні файли (CSS, JS, зображення)
└── Program.cs           # Entry point, DI-реєстрація, middleware
```

**Стек технологій:**

| Компонент | Технологія |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| ORM | Entity Framework Core (SQL Server) |
| Автентифікація | ASP.NET Core Identity |
| PDF-генерація | QuestPDF |
| CSV-імпорт | CsvHelper |
| AI-інтеграція | Google Gemini 2.5 Flash |
| Telegram | Telegram Bot API (long-polling) |
| Email | SMTP (кастомний `AuthEmailSender`) |

---

## 🗄️ Шар даних (Data Layer)

### ApplicationDbContext

Успадковує `IdentityDbContext<ApplicationUser>`. Реєструє всі DbSet-и та налаштовує зв'язки через Fluent API у `OnModelCreating`.

```
ApplicationDbContext
├── DbSet<BusinessAccount>    BusinessAccounts
├── DbSet<ProductCategory>    ProductCategories
├── DbSet<Product>            Products
├── DbSet<Transaction>        Transactions
├── DbSet<ExpenseCategory>    ExpenseCategories
├── DbSet<Premises>           Premises
├── DbSet<UtilityType>        UtilityTypes
└── DbSet<UtilityRecord>      UtilityRecords
```

---

## 📦 Моделі (Entities)

### Зв'язки між сутностями

```
ApplicationUser (IdentityUser)
│   ├── FullName, Organization, About
│   ├── TelegramChatId, TelegramLinkToken, TelegramLinkTokenExpiry
│   └── [1:N] ──► BusinessAccount
│
BusinessAccount
│   ├── Id (Guid PK), Name, Description, Address
│   ├── RegistrationCode (ЄДРПОУ / ІПН), LogoPath
│   ├── OwnerId (FK → ApplicationUser)  [CASCADE DELETE]
│   ├── [1:N] ──► ProductCategory       [RESTRICT]
│   ├── [1:N] ──► Product               [RESTRICT]
│   ├── [1:N] ──► Transaction           [CASCADE DELETE]
│   ├── [1:N] ──► ExpenseCategory       [RESTRICT]
│   ├── [1:N] ──► Premises              [CASCADE DELETE]
│   └── [1:N] ──► UtilityType           [CASCADE DELETE]
│
ProductCategory
│   ├── BusinessAccountId (FK)
│   └── [1:N] ──► Product               [SET NULL]
│
Product
│   ├── BusinessAccountId (FK), ProductCategoryId (FK, nullable)
│   ├── Name, Description, Price, Discount
│   └── FinalPrice (computed: Price * (1 - Discount/100))
│
Transaction
│   ├── BusinessAccountId (FK) [CASCADE DELETE]
│   ├── ProductId (FK, nullable) [SET NULL]
│   ├── ExpenseCategoryId (FK, nullable) [SET NULL]
│   ├── TransactionType (enum: Income=1 | Expense=2)
│   ├── Date, Quantity, UnitPrice, TotalAmount
│   ├── Description
│   └── ImportBatchId (Guid?, для undo CSV-імпорту)
│
ExpenseCategory
│   ├── BusinessAccountId (FK)
│   └── [1:N] ──► Transaction [SET NULL]
│
Premises (Приміщення/Об'єкт)
│   ├── BusinessAccountId (FK)
│   └── [1:N] ──► UtilityRecord [RESTRICT]
│
UtilityType (Тип комунальної послуги)
│   ├── BusinessAccountId (FK)
│   └── [1:N] ──► UtilityRecord [RESTRICT]
│
UtilityRecord (Місячний запис оплати)
    ├── PremisesId (FK), UtilityTypeId (FK)
    ├── Year, Month, Amount, Note, IsPaid, PaidAt
    ├── LinkedTransactionId (Guid?) — зв'язок із Transaction при оплаті
    └── Унікальний індекс: (PremisesId, UtilityTypeId, Year, Month)
```

---

## ⚙️ Сервісний шар (Services)

### Інтерфейси та реалізації

| Інтерфейс | Реалізація | DI Lifetime | Опис |
|---|---|---|---|
| `IBusinessService` | `BusinessService` | Scoped | CRUD для бізнес-акаунтів |
| `IProductService` | `ProductService` | Scoped | CRUD для товарів |
| `IProductCategoryService` | `ProductCategoryService` | Scoped | CRUD для категорій товарів |
| `ITransactionService` | `TransactionService` | Scoped | CRUD для транзакцій |
| `IAnalyticsService` | `AnalyticsService` | Scoped | Розрахунок метрик, порівняння, поради |
| `IAiService` | `AiService` | HttpClient (Scoped) | Генерація AI-звіту через Google Gemini |
| `IEmailSender` | `AuthEmailSender` | Transient | Відправка HTML-листів через SMTP |
| — | `TelegramReportService` | HostedService | Фоновий сервіс Telegram |

---

### AnalyticsService (детально)

Основний сервіс аналітики. Залежить тільки від `ApplicationDbContext`.

```
AnalyticsService
│
├── GetUserBusinessOverviewsAsync(userId)
│     └─ Повертає List<BusinessOverviewDTO> з метриками (Прибуток, Оборот, Витрати)
│
├── GetMetricsForBusinessAsync(businessId, startDate, endDate)
│     └─ Повертає Dictionary<string, decimal>:
│           Прибуток, Оборот, Витрати, Середній чек
│           + Найприбутковіший товар, Найприбутковіша категорія
│
├── CompareBusinessesAsync(businessIds, startDate, endDate)
│     └─ Повертає CombinedAnalyticsDTO:
│           ├── Businesses[]          — метрики по кожному бізнесу
│           ├── ProfitTrends{}        — щоденний тренд прибутку
│           ├── TopProducts{}         — топ-товар на бізнес
│           ├── TopCategories{}       — топ-категорія на бізнес
│           └── CategoryDistribution{}— розподіл доходу по категоріях
│
├── GetSuggestionsAsync(userId)
│     └─ Генерує текстові поради за 30 днів:
│           • Від'ємний прибуток
│           • Малий середній чек (<100₴)
│           • Падіння прибутку >25%
│           • Залежність від одного товару (>60% доходу)
│           • Витрати >70% обороту
│           • Відсутність транзакцій
│
└── CompareBusinessesForTipsAsync(userId / businessIds, ...)
      └─ Варіант CompareBusinesses з фокусом на поради для AI-консультанта
```

---

### AiService

Підключається до **Google Gemini 2.5 Flash** через REST API.

```
AiService.GenerateFinancialReportAsync(businessName, income, expense, txCount, topCategory)
│
└─ Формує prompt українською мовою як CFO-аналітик
   → POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent
   → Повертає HTML-форматований текст для відображення у View
```

API-ключ зберігається в `appsettings.json` → `GeminiApiKey`.

---

### TelegramReportService (BackgroundService)

Три паралельні асинхронні цикли:

```
TelegramReportService : BackgroundService
│
├── LOOP 1 — RunPollingLoopAsync (кожні 3 секунди)
│     └── PollAndHandleUpdatesAsync
│           ├── GET /getUpdates?offset=...&timeout=25
│           └── Якщо /start TOKEN → HandleLinkTokenAsync
│                 └── Знаходить юзера за TelegramLinkToken
│                     → Прив'язує TelegramChatId
│                     → Відправляє підтвердження
│
├── LOOP 2 — RunDailyReportLoopAsync (щодня о ReportHour)
│     └── SendDailyReportsToAllUsersAsync
│           ├── Знаходить юзерів із TelegramChatId != null
│           ├── Підтягує транзакції за сьогодні
│           └── BuildReportMessage → SendMessageAsync
│                 Markdown: Дохід, Витрати, Прибуток, кількість транзакцій
│
└── LOOP 3 — RunWeeklyUnpaidExpensesLoopAsync (щопонеділка о 09:00)
      └── SendUnpaidExpensesRemindersAsync
            ├── Знаходить UtilityRecords де IsPaid == false
            └── BuildUnpaidExpensesMessage → SendMessageAsync
                  Групує по бізнесу → Приміщення → Послуга → Місяць/Рік/Сума
```

**Налаштування** (`appsettings.json` → `TelegramSettings`):
- `BotToken` — токен бота
- `BotUsername` — @username бота
- `ReportHour` — година відправки щоденного звіту

---

### Патерн Strategy — IBusinessMetric

```
IBusinessMetric (інтерфейс)
│   ├── string Name { get; }
│   ├── string DisplayName { get; }
│   └── Task<decimal> CalculateAsync(businessId, startDate, endDate)
│
├── ProfitMetric   → Income - Expenses
├── RevenueMetric  → Sum(Income transactions)
└── ExpensesMetric → Sum(Expense transactions)
```

Усі три реєструються як `IBusinessMetric` через `AddScoped`. Дозволяє легко додавати нові метрики без зміни ядра системи.

---

## 🎮 Контролери (Controllers)

### Карта маршрутів

| Контролер | Маршрут | Основні дії |
|---|---|---|
| `HomeController` | `/` | Index, Privacy |
| `AccountController` | `/Account/` | Register, Login, Logout, Profile, ChangePassword, ForgotPassword, Telegram-прив'язка |
| `BusinessController` | `/Business/` | Index, Create, Edit, Delete |
| `ProductController` | `/Product/` | CRUD товарів |
| `ProductCategoryController` | `/ProductCategory/` | CRUD категорій |
| `TransactionController` | `/Transaction/` | Index (з пагінацією), Create, Delete, UploadCsv, UndoImport |
| `AnalyticsController` | `/Analytics/` | Index, Compare, GetAnalyticsData (JSON API), GeneratePdf, GetUserTips |
| `AiConsultantController` | `/AiConsultant/` | Генерація AI-звіту |
| `UtilitiesController` | `/Utilities/` | CRUD приміщень, типів послуг, місячних записів, MarkPaid/Unpaid |
| `DashboardController` | `/Dashboard/` | Головна аналітична панель |
| `AdminController` | `/Admin/` | Адмін-панель (роль Admin) |

---

### AccountController — Telegram flow

```
Користувач → Profile → "Прив'язати Telegram"
     │
     ├── POST /Account/GenerateTelegramLink
     │     └── user.TelegramLinkToken = Guid (expires 15 хв)
     │         → DeepLink: https://t.me/BotName?start=TOKEN
     │
     └── Користувач клікає у браузері → відкривається Telegram бот
           └── /start TOKEN  (обробляє TelegramReportService LOOP 1)
                 └── user.TelegramChatId = chatId  ✓
```

---

### TransactionController — CSV-імпорт

```
POST /Transaction/UploadCsv (file, businessId)
│
├── CsvReader → List<TransactionCsvRecord>
│   (формат: Date;Amount;Description, локаль uk-UA, роздільник ";")
│   Amount > 0 → Income, Amount < 0 → Expense
│
├── Генерує ImportBatchId (Guid) для групи
├── AddRangeAsync → SaveChangesAsync
└── TempData["LastBatchId"] → можливість Undo

POST /Transaction/UndoImport (batchId, businessId)
└── Видаляє всі транзакції з даним ImportBatchId
```

---

### AnalyticsController — PDF-генерація

```
POST /Analytics/GeneratePdf (BusinessIds[], StartDate, EndDate)
│
├── Завантажує BusinessAccounts із БД
├── Обчислює поточний та попередній період
├── currentTx / prevTx → Income, Expenses, Profit
├── UtilityRecords за місяці в діапазоні
│
└── QuestPDF Document.Create()
      ├── Header: назва, дати, бізнеси
      ├── Секція 1: Оборот / Витрати / Чистий прибуток
      ├── Секція 2: Таблиця операційних витрат (Utilities)
      └── Секція 3: Динаміка % (порівняно з попереднім)
      → File(pdfBytes, "application/pdf", filename)
```

---

### UtilitiesController — MarkPaid flow

```
POST /Utilities/MarkPaid (id, businessId)
│
├── Завантажує UtilityRecord + Premises + UtilityType
├── record.IsPaid = true, record.PaidAt = UtcNow
│
├── Автоматично створює Transaction:
│     TransactionType = Expense
│     Date = поточний місяць → DateTime.Now
│           або минулий місяць → 1-ше число місяця
│     TotalAmount = record.Amount
│     Description = "{UtilityType} — {Premises} ({Місяць} {Рік})"
│
├── record.LinkedTransactionId = transaction.Id
└── SaveChangesAsync
```

При `MarkUnpaid` — пов'язана транзакція видаляється.

---

## 🔄 DI-граф залежностей (Program.cs)

```
DbContext
└── ApplicationDbContext → SQL Server

Identity
└── ApplicationUser + IdentityRole
      └── Cookie auth (LoginPath: /Account/Login)

Scoped Services
├── IBusinessService        → BusinessService
├── IProductService         → ProductService
├── IProductCategoryService → ProductCategoryService
├── ITransactionService     → TransactionService
├── IAnalyticsService       → AnalyticsService
└── IBusinessMetric[]       → ProfitMetric, ExpensesMetric, RevenueMetric

HttpClient
└── IAiService → AiService (typed HttpClient)

Transient
└── IEmailSender → AuthEmailSender

Configuration (IOptions<T>)
├── SmtpSettings  → секція SmtpSettings (appsettings.json)
└── TelegramSettings → секція TelegramSettings (appsettings.json)

HostedService
└── TelegramReportService (BackgroundService)
```

---

## 📬 Email-підтвердження (AccountController)

```
Реєстрація → CreateAsync → GenerateEmailConfirmationTokenAsync
→ AuthEmailSender.SendEmailAsync (HTML-лист, брендований стиль)
→ Redirect → /Account/VerifyEmail?email=...
→ Код вводиться вручну (не через URL-посилання)
→ ConfirmEmailAsync + SignInAsync

Відновлення пароля → GeneratePasswordResetTokenAsync
→ callbackUrl → /Account/ResetPassword?email=...&token=...
→ ResetPasswordAsync

Зміна Email → 6-значний код (Random 100000-999999)
→ Зберігається в TempData (сесія) — не в БД
→ Підтверджується на /Account/ConfirmEmailChange
```

---

## 🔑 Авторизація та ролі

```
Ролі (ініціалізуються у SeedRoles.Initialize):
├── "User"    — стандартний користувач (присвоюється при реєстрації)
├── "ProUser" — розширений доступ (змінюється адміністратором)
└── "Admin"   — адміністратор (AdminController, [Authorize(Roles="Admin")])

AdminController:
├── Index(search) — перегляд всіх юзерів з їхніми ролями + пошук
└── ChangeRole(userId, newRole) — зміна ролі юзера:
      └── RemoveFromRolesAsync → AddToRoleAsync

Захист ресурсів:
├── [Authorize]                 — більшість контролерів
├── [Authorize(Roles="Admin")]  — AdminController
└── Tenant isolation — userId завжди перевіряється в сервісах:
      GetUserBusinessesAsync(userId) → LINQ .Where(b => b.OwnerId == userId)
```

---

## 📊 DTO-шар

| DTO | Призначення |
|---|---|
| `BusinessOverviewDTO` | Огляд бізнесу на головній сторінці аналітики (Id, Name, ImageUrl, Metrics) |
| `BusinessMetricsDTO` | Метрики одного бізнесу в порівнянні (Id, Name, Metrics dictionary) |
| `CombinedAnalyticsDTO` | Агрегований результат порівняння: Businesses, ProfitTrends, TopProducts, TopCategories, CategoryDistribution |
| `ProfitTrendPoint` | Точка тренду: Date + Profit |
| `CategoryDistributionDTO` | Розподіл доходу по категоріях: BusinessName + Data{category→amount} |
| `TransactionCsvRecord` | Рядок CSV-файлу: Date, Amount, Description |
| `BusinessMetricsDTO` | Компактна модель метрик для передачі в JSON API |

---

## 🔗 Взаємодія модулів (схема потоків)

```
                     ┌─────────────────────────────────┐
                     │           БРАУЗЕР/КЛІЄНТ         │
                     └────────────┬────────────────────┘
                                  │ HTTP
                     ┌────────────▼────────────────────┐
                     │         CONTROLLERS              │
                     │  Account │ Business │ Analytics  │
                     │  Transaction │ Utilities │ AI    │
                     └────────────┬────────────────────┘
                                  │ DI
             ┌────────────────────┼──────────────────────┐
             │                    │                      │
    ┌────────▼───────┐  ┌────────▼───────┐  ┌──────────▼────────┐
    │ AnalyticsService│  │ Business/Prod/ │  │     AiService     │
    │                 │  │ Transaction/   │  │  (Google Gemini)   │
    │ • Metrics       │  │ Category Svcs  │  └───────────────────┘
    │ • Compare       │  └────────┬───────┘
    │ • Tips          │           │
    └────────┬────────┘           │
             │              ┌─────▼──────────────────────┐
             └──────────────►    ApplicationDbContext     │
                            │    (Entity Framework Core)  │
                            │                             │
                            │  BusinessAccounts           │
                            │  Products / Categories      │
                            │  Transactions               │
                            │  Premises / UtilityRecords  │
                            └─────────────┬───────────────┘
                                          │
                            ┌─────────────▼───────────────┐
                            │        SQL SERVER            │
                            └─────────────────────────────┘

 ┌───────────────────────────────────────────────────────┐
 │               TelegramReportService                   │
 │  (IHostedService, фоновий процес)                     │
 │  ├── Loop 1: Polling getUpdates → прив'язка ChatId    │
 │  ├── Loop 2: Щоденний фінансовий звіт                 │
 │  └── Loop 3: Щотижневе нагадування (неоплачені)       │
 │                                                       │
 │  Використовує IServiceScopeFactory → ApplicationDbCtx│
 └───────────────────────────────────────────────────────┘
```

---

## 📁 Ключові файли

| Файл | Роль |
|---|---|
| [Program.cs](BusinessAnalytics/Program.cs) | Entry point, DI, middleware pipeline |
| [ApplicationDbContext.cs](BusinessAnalytics/Data/ApplicationDbContext.cs) | EF Core DbContext, Fluent API конфігурація |
| [AnalyticsService.cs](BusinessAnalytics/Services/Realization/AnalyticsService%20.cs) | Ядро аналітики |
| [TelegramReportService.cs](BusinessAnalytics/Services/Realization/TelegramReportService.cs) | Фоновий Telegram-бот |
| [AiService.cs](BusinessAnalytics/Services/Realization/AiService.cs) | Інтеграція з Gemini |
| [AccountController.cs](BusinessAnalytics/Controllers/AccountController.cs) | Автентифікація + Telegram-прив'язка |
| [AnalyticsController.cs](BusinessAnalytics/Controllers/AnalyticsController%20.cs) | Analytics API + PDF-генерація |
| [TransactionController.cs](BusinessAnalytics/Controllers/TransactionController.cs) | Транзакції + CSV-імпорт |
| [UtilitiesController.cs](BusinessAnalytics/Controllers/UtilitiesController.cs) | Комунальні витрати + auto-Transaction |

---

## ⚙️ Конфігурація (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=BusinessAnalytics;..."
  },
  "GeminiApiKey": "YOUR_GEMINI_API_KEY",
  "SmtpSettings": {
    "Host": "smtp.example.com",
    "Port": 587,
    "UserName": "noreply@example.com",
    "Password": "...",
    "FromEmail": "noreply@example.com",
    "FromName": "Business Analytics"
  },
  "TelegramSettings": {
    "BotToken": "YOUR_BOT_TOKEN",
    "BotUsername": "YourBotName",
    "ReportHour": 20
  }
}
```

---

*Документ згенеровано автоматично на основі аналізу кодової бази — 2026-05-22*
