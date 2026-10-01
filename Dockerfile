# Build stage: the SDK image has the tools to restore and publish the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so this layer is cached until the project file changes.
COPY BudgetApi/BudgetApi.csproj BudgetApi/
RUN dotnet restore BudgetApi/BudgetApi.csproj

COPY . .
RUN dotnet publish BudgetApi/BudgetApi.csproj -c Release -o /app --no-restore

# Runtime stage: the smaller ASP.NET image only runs the published app.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# The ASP.NET images listen on port 8080 by default and provide a non-root user.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "BudgetApi.dll"]
