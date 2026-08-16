# syntax=docker/dockerfile:1

FROM node:22-alpine AS angular-build
ARG CHATBOT_PUBLIC_URL=http://euro13:81
WORKDIR /src
COPY backup.web.api.client/package.json backup.web.api.client/package-lock.json* ./
RUN npm ci
COPY backup.web.api.client/ ./
RUN sed -i "s|CHATBOT_PUBLIC_URL_PLACEHOLDER|${CHATBOT_PUBLIC_URL}|g" src/environments/environment.prod.ts
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src
COPY Backup.Web.Api.Server/Backup.Web.Api.Server.csproj Backup.Web.Api.Server/
RUN dotnet restore Backup.Web.Api.Server/Backup.Web.Api.Server.csproj
COPY Backup.Web.Api.Server/ Backup.Web.Api.Server/
RUN mkdir -p Backup.Web.Api.Server/wwwroot
COPY --from=angular-build /src/dist/backup.web.api.client/browser/ Backup.Web.Api.Server/wwwroot/
RUN dotnet publish Backup.Web.Api.Server/Backup.Web.Api.Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-fra tesseract-ocr-eng curl \
        python3 python3-venv \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /app/venv

COPY docs/requirements-ktype-sync.txt /tmp/requirements-ktype-sync.txt
RUN /app/venv/bin/pip install --no-cache-dir -r /tmp/requirements-ktype-sync.txt \
    && rm /tmp/requirements-ktype-sync.txt

COPY --from=dotnet-build /app/publish .
RUN mkdir -p /app/Storage /app/docs /app/Backup.Web.Api.Server
COPY docs/sync_ktype_vehicle.py docs/sync_rapidapi_poc.py /app/docs/
COPY Backup.Web.Api.Server/appsettings.Docker.json /app/Backup.Web.Api.Server/appsettings.json

ENV ASPNETCORE_URLS=http://+:8080
ENV RapidApi__PythonExecutable=/app/venv/bin/python
ENV RapidApi__KTypeSyncScriptPath=/app/docs/sync_ktype_vehicle.py
EXPOSE 8080

ENTRYPOINT ["dotnet", "Backup.Web.Api.Server.dll"]
