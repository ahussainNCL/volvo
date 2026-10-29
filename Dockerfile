# syntax=docker/dockerfile:1
FROM node:24.19.0-bookworm-slim AS frontend
WORKDIR /build/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then NODE_EXTRA_CA_CERTS=/run/secrets/proxy_ca npm ci --no-fund --strict-ssl=true; else npm ci --no-fund; fi
COPY frontend/ ./
ENV VITE_NORDLINK_HOSTING=cloud
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /build
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/Nordlink.Contracts/ src/Nordlink.Contracts/
COPY src/Nordlink.Cloud/ src/Nordlink.Cloud/
COPY --chmod=0644 docs/feature-register.json docs/feature-register.json
RUN --mount=type=secret,id=ca_bundle \
    if [ -f /run/secrets/ca_bundle ]; then SSL_CERT_FILE=/run/secrets/ca_bundle dotnet restore src/Nordlink.Cloud --locked-mode --configfile NuGet.Config; else dotnet restore src/Nordlink.Cloud --locked-mode --configfile NuGet.Config; fi
COPY --from=frontend /build/src/Nordlink.Cloud/wwwroot/ src/Nordlink.Cloud/wwwroot/
RUN dotnet publish src/Nordlink.Cloud --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /publish/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Nordlink.Cloud.dll"]
