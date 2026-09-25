# Étape 1 : Compilation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/CtrRoster.Domain/*.csproj src/CtrRoster.Domain/
COPY src/CtrRoster.Application/*.csproj src/CtrRoster.Application/
COPY src/CtrRoster.Infrastructure/*.csproj src/CtrRoster.Infrastructure/
COPY src/CtrRoster.Presentation/*.csproj src/CtrRoster.Presentation/

RUN dotnet restore src/CtrRoster.Presentation/CtrRoster.Presentation.csproj

COPY . .
WORKDIR /source/src/CtrRoster.Presentation
RUN dotnet publish -c Release -o /app --no-restore

# Étape 2 : Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y libsqlite3-0 tzdata && rm -rf /var/lib/apt/lists/*
ENV TZ=Europe/Paris

COPY --from=build /app .
VOLUME /app/data

ENTRYPOINT ["dotnet", "CtrRoster.Presentation.dll"]
