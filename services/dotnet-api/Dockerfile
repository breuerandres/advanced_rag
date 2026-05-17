FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AdvancedRag.sln ./
COPY src/AdvancedRag.Api/AdvancedRag.Api.csproj src/AdvancedRag.Api/
COPY src/AdvancedRag.App/AdvancedRag.App.csproj src/AdvancedRag.App/
COPY src/AdvancedRag.Domain/AdvancedRag.Domain.csproj src/AdvancedRag.Domain/
COPY src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj src/AdvancedRag.Infrastructure/
COPY tests/AdvancedRag.Api.Tests/AdvancedRag.Api.Tests.csproj tests/AdvancedRag.Api.Tests/
COPY tests/AdvancedRag.App.Tests/AdvancedRag.App.Tests.csproj tests/AdvancedRag.App.Tests/
COPY tests/AdvancedRag.Infrastructure.Tests/AdvancedRag.Infrastructure.Tests.csproj tests/AdvancedRag.Infrastructure.Tests/

RUN dotnet restore AdvancedRag.sln

COPY . .
RUN dotnet publish src/AdvancedRag.Api/AdvancedRag.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AdvancedRag.Api.dll"]
