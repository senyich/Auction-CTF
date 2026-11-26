FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Auction/Domain/Domain.csproj Auction/Domain/
COPY Auction/Application/Application.csproj Auction/Application/
COPY Auction/Infrastructure/Infrastructure.csproj Auction/Infrastructure/
COPY Auction/Program/Program.csproj Auction/Program/
RUN dotnet restore Auction/Program/Program.csproj
COPY Auction/ Auction/
RUN dotnet publish Auction/Program/Program.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out/ ./
RUN mkdir data && chown app:app data
COPY --chown=app:app Auction/Program/auction.db data/
ENV ConnectionStrings__SqliteConnectionString="Data Source=/app/data/auction.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Program.dll"]
