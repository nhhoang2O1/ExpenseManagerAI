FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ExpenseManager.sln ./
COPY src/ExpenseManager.Api/ExpenseManager.Api.csproj src/ExpenseManager.Api/
RUN dotnet restore src/ExpenseManager.Api/ExpenseManager.Api.csproj
COPY src/ExpenseManager.Api/ src/ExpenseManager.Api/
RUN dotnet publish src/ExpenseManager.Api/ExpenseManager.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN mkdir -p /app/storage/receipts
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV OCR_SERVICE_BASE_URL=http://ocr-service:8000
ENV Storage__ReceiptPath=/app/storage/receipts
ENV Database__ApplyMigrations=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExpenseManager.Api.dll"]
