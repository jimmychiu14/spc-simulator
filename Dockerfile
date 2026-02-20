FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Spc.Api/Spc.Api/*.csproj ./
RUN dotnet restore

COPY Spc.Api/Spc.Api/ ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENV PORT=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Spc.Api.dll"]
