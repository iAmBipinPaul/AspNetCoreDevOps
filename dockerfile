FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.19 AS buildimg
WORKDIR /app
COPY . .
WORKDIR /app/src/AspNetCoreDevOps.Api
RUN dotnet publish -c  Release -o output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine3.19
WORKDIR output
COPY --from=buildimg /app/src/AspNetCoreDevOps.Api/output .

LABEL org.opencontainers.image.source https://github.com/iAmBipinPaul/AspNetCoreDevOps

ENTRYPOINT ["dotnet","AspNetCoreDevOps.Api.dll"]
