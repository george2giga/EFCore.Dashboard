FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY . .
RUN dotnet publish samples/EFCore.Dashboard.BasicSample/EFCore.Dashboard.BasicSample.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Default="Data Source=/tmp/efcore-dashboard-sample.db"

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet EFCore.Dashboard.BasicSample.dll --urls http://0.0.0.0:${PORT:-8080}"]
