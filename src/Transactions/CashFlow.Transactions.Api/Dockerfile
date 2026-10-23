FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/BuildingBlocks/ ./src/BuildingBlocks/
COPY src/Transactions/ ./src/Transactions/
RUN dotnet restore src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj --locked-mode
RUN dotnet publish src/Transactions/CashFlow.Transactions.Api/CashFlow.Transactions.Api.csproj \
    --configuration Release --no-restore --output /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CashFlow.Transactions.Api.dll"]
