# Imagem compilada no GitHub Actions — o ZimaOS baixa somente a imagem pronta.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY XtreamStrmOrganizer.csproj ./
RUN dotnet restore XtreamStrmOrganizer.csproj
COPY . .
RUN dotnet publish XtreamStrmOrganizer.csproj -c Release -o /out --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
LABEL org.opencontainers.image.source="https://github.com/lucassfontes/Xtream_STRM_Organizer"
LABEL org.opencontainers.image.title="Xtream STRM Organizer"
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    OUTPUT_ROOT=/media \
    CONFIG_FILE=/app/data/config.json \
    STRM_WRITE_WORKERS=4
EXPOSE 8080
ENTRYPOINT ["dotnet", "XtreamStrmOrganizer.dll"]
