FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for optimal layer caching (like copying package.json before npm install)
COPY ["src/NovaWallet.Domain/NovaWallet.Domain.csproj", "src/NovaWallet.Domain/"]
COPY ["src/NovaWallet.Infrastructure/NovaWallet.Infrastructure.csproj", "src/NovaWallet.Infrastructure/"]
COPY ["src/NovaWallet.Api/NovaWallet.Api.csproj", "src/NovaWallet.Api/"]

# Restore dependencies
RUN dotnet restore "src/NovaWallet.Api/NovaWallet.Api.csproj"

# Copy full source and build
COPY ["src/", "src/"]
WORKDIR "/src/src/NovaWallet.Api"
RUN dotnet build "NovaWallet.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NovaWallet.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NovaWallet.Api.dll"]

