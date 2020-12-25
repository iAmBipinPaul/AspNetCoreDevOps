FROM mcr.microsoft.com/dotnet/sdk:5.0.101-alpine3.12-amd64 AS buildimg
WORKDIR /app
COPY . .
WORKDIR /app/src/AspNetCoreDevOps.Api
RUN dotnet publish -c  Release -o output

FROM mcr.microsoft.com/dotnet/aspnet:5.0.1-alpine3.12-amd64
WORKDIR output
COPY --from=buildimg /app/src/AspNetCoreDevOps.Api/output .
ENTRYPOINT ["dotnet","AspNetCoreDevOps.Api.dll"]
