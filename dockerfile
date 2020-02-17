FROM mcr.microsoft.com/dotnet/core/sdk:3.1.101-alpine3.10 AS buildimg
WORKDIR /app
COPY . .
WORKDIR /app/src/AspNetCoreDevOps.Api
RUN dotnet publish -c  Release -o output

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.1-alpine3.10
WORKDIR output
COPY --from=buildimg /app/src/AspNetCoreDevOps.Api/output .
ENTRYPOINT ["dotnet","AspNetCoreDevOps.Api.dll"]
