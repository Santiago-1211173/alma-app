# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# copia TUDO do repo para /src
COPY . .

# publica explicitamente o projeto WebApi
RUN dotnet publish src/WebApi/AlmaApp.WebApi.csproj -c Release -o /out

# ---------- run ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .

# Só HTTP no container (evita dores com certs em dev)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet","AlmaApp.WebApi.dll"]
