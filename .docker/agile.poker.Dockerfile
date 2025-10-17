FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

EXPOSE 7080
EXPOSE 7081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG BUILD_CONFIGURATION=Release
COPY ["src/", "src/"]
RUN dotnet restore "src/CodeChavez.AgilePoker/CodeChavez.AgilePoker.csproj"
COPY . .
WORKDIR "/src/src/CodeChavez.AgilePoker"
RUN dotnet build "./CodeChavez.AgilePoker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CodeChavez.AgilePoker.csproj" -c $BUILD_CONFIGURATION -o /app/publish --self-contained true -r linux-x64

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:7080;http://+:7081
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "CodeChavez.AgilePoker.dll"]