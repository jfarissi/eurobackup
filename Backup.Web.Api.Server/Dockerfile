# syntax=docker/dockerfile:1

FROM node:22-alpine AS angular-build
WORKDIR /src
COPY backup.web.api.client/package.json backup.web.api.client/package-lock.json* ./
RUN npm ci
COPY backup.web.api.client/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-build
WORKDIR /src
COPY Backup.Web.Api.Server/Backup.Web.Api.Server.csproj Backup.Web.Api.Server/
RUN dotnet restore Backup.Web.Api.Server/Backup.Web.Api.Server.csproj
COPY Backup.Web.Api.Server/ Backup.Web.Api.Server/
RUN mkdir -p Backup.Web.Api.Server/wwwroot
COPY --from=angular-build /src/dist/backup.web.api.client/browser/ Backup.Web.Api.Server/wwwroot/
RUN dotnet publish Backup.Web.Api.Server/Backup.Web.Api.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-fra tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*

COPY --from=dotnet-build /app/publish .
RUN mkdir -p /app/Storage

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Backup.Web.Api.Server.dll"]
