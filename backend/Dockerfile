# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) using the solution + project files.
COPY DevDocsAI.slnx Directory.Build.props ./
COPY src/DevDocsAI.Domain/*.csproj src/DevDocsAI.Domain/
COPY src/DevDocsAI.Application/*.csproj src/DevDocsAI.Application/
COPY src/DevDocsAI.Infrastructure/*.csproj src/DevDocsAI.Infrastructure/
COPY src/DevDocsAI.Api/*.csproj src/DevDocsAI.Api/
COPY tests/DevDocsAI.UnitTests/*.csproj tests/DevDocsAI.UnitTests/
COPY tests/DevDocsAI.IntegrationTests/*.csproj tests/DevDocsAI.IntegrationTests/
RUN dotnet restore src/DevDocsAI.Api/DevDocsAI.Api.csproj

# Build & publish the API.
COPY . .
RUN dotnet publish src/DevDocsAI.Api/DevDocsAI.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevDocsAI.Api.dll"]
