FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Botijas.sln ./
COPY src/Botijas.Api/Botijas.Api.csproj src/Botijas.Api/
COPY src/Botijas.Application/Botijas.Application.csproj src/Botijas.Application/
COPY src/Botijas.Infrastructure/Botijas.Infrastructure.csproj src/Botijas.Infrastructure/
COPY src/Botijas.Domain/Botijas.Domain.csproj src/Botijas.Domain/

RUN dotnet restore src/Botijas.Api/Botijas.Api.csproj

COPY src ./src
RUN dotnet publish src/Botijas.Api/Botijas.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Botijas.Api.dll"]
