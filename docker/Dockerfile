FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY docker/MonitorSerialDocker.csproj ./
RUN dotnet restore
COPY docker/Program.cs ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Copiar los activos locales de Materialize CSS y JS del proyecto principal y el script.sql
COPY MonitorSerial/Content ./wwwroot/Content
COPY MonitorSerial/Scripts ./wwwroot/Scripts
COPY script.sql ./script.sql
EXPOSE 8080
ENTRYPOINT ["dotnet", "MonitorSerialDocker.dll"]
