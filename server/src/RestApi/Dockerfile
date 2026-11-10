FROM mcr.microsoft.com/dotnet/sdk:11.0 AS builder

WORKDIR /server

COPY . .

RUN dotnet restore src/RestApi && dotnet publish src/RestApi -o /app/publish -c Release

FROM mcr.microsoft.com/dotnet/aspnet:11.0

WORKDIR /app

COPY --from=builder /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "RestApi.dll"]
