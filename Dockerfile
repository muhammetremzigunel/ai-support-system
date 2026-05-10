# ============================================
# STAGE 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce sadece csproj kopyala → NuGet restore cache'le
COPY ai-support-system/ai-support-system.csproj ai-support-system/
RUN dotnet restore ai-support-system/ai-support-system.csproj

# Tüm kaynak kodunu kopyala
COPY ai-support-system/ ai-support-system/

# Publish (Release modunda, self-contained değil)
RUN dotnet publish ai-support-system/ai-support-system.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================
# STAGE 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Güvenlik: root olmayan kullanıcı ile çalıştır
USER $APP_UID

# Publish çıktısını kopyala
COPY --from=build /app/publish .

# Container içinde 8080 portunu aç
EXPOSE 8080

# Ortam değişkenleri
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ai-support-system.dll"]
