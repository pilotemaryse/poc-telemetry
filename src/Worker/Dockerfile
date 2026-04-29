FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Domain/Domain.csproj Domain/
COPY src/Worker/Worker.csproj Worker/
RUN dotnet restore Worker/Worker.csproj
COPY src/Domain Domain/
COPY src/Worker Worker/
RUN dotnet publish Worker/Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DockerCrudDemo.Worker.dll"]
