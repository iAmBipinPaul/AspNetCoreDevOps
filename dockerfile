FROM mcr.microsoft.com/dotnet/sdk:8.0.203-alpine3.19-amd64 AS buildimg
WORKDIR /app
COPY . .
WORKDIR /app/src/AspNetCoreDevOps.Api
RUN dotnet publish -c  Release -o output

FROM mcr.microsoft.com/dotnet/aspnet:8.0.3-alpine3.19-amd64
WORKDIR output
COPY --from=buildimg /app/src/AspNetCoreDevOps.Api/output .
ENTRYPOINT ["dotnet","AspNetCoreDevOps.Api.dll"]
