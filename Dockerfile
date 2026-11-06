# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DCDSS/DCDSS.csproj", "DCDSS/"]
RUN dotnet restore "DCDSS/DCDSS.csproj"
COPY . .
WORKDIR "/src/DCDSS"
RUN dotnet publish "DCDSS.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DCDSS.dll"]
