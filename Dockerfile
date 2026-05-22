# Використовуємо офіційний образ ASP.NET Core 9.0 для запуску програми
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Використовуємо образ .NET SDK 9.0 для збірки програми
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо файл проєкту і відновлюємо залежності (NuGet пакети)
COPY ["BusinessAnalytics/BusinessAnalytics.csproj", "BusinessAnalytics/"]
RUN dotnet restore "BusinessAnalytics/BusinessAnalytics.csproj"

# Копіюємо всі інші файли проєкту
COPY . .
WORKDIR "/src/BusinessAnalytics"

# Збираємо проєкт
RUN dotnet build "BusinessAnalytics.csproj" -c Release -o /app/build

# Публікуємо проєкт (оптимізуємо для продакшену)
FROM build AS publish
RUN dotnet publish "BusinessAnalytics.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Фінальний етап: беремо базовий образ і копіюємо туди опубліковані файли
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BusinessAnalytics.dll"]
