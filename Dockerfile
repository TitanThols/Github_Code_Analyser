# syntax=docker/dockerfile:1.7
# Single production image: the API serves the built React app from wwwroot, so the
# browser talks to one origin (first-party cookies, no CORS, one service to deploy).

# ---- 1. Build the SPA -------------------------------------------------------
FROM node:22-alpine AS web
WORKDIR /web
COPY oss-analyser-frontend/package.json oss-analyser-frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY oss-analyser-frontend/ ./
RUN npm run build

# ---- 2. Publish the API with the SPA embedded --------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY OSSDependencyAnalyser.API/GithubAnalyser.Api.csproj OSSDependencyAnalyser.API/
RUN dotnet restore OSSDependencyAnalyser.API/GithubAnalyser.Api.csproj
COPY OSSDependencyAnalyser.API/ OSSDependencyAnalyser.API/
COPY --from=web /web/dist OSSDependencyAnalyser.API/wwwroot/
RUN dotnet publish OSSDependencyAnalyser.API/GithubAnalyser.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- 3. Runtime ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=0 \
    PORT=8080
COPY --from=api /app ./
# Built-in non-root user of the official .NET images.
USER $APP_UID
EXPOSE 8080
# PaaS platforms (Render, Railway, Fly, Cloud Run) inject $PORT; default to 8080 elsewhere.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT} exec dotnet OSSDependencyAnalyzer.Api.dll"]
