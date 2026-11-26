FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DataServer/Domain/Domain.csproj DataServer/Domain/
COPY DataServer/Application/Application.csproj DataServer/Application/
COPY DataServer/Infrastructure/Infrastructure.csproj DataServer/Infrastructure/
COPY DataServer/Program/Program.csproj DataServer/Program/
RUN dotnet restore DataServer/Program/Program.csproj
COPY DataServer/ DataServer/
RUN dotnet publish DataServer/Program/Program.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir data && chown app:app data
COPY --chown=app:app DataServer/Program/data.db data/
ENV ConnectionStrings__SqliteConnectionString="Data Source=/app/data/data.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Program.dll"]
