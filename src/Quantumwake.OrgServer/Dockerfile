# The org server's container: one process, one volume, one port.
# Build from the repository root:
#   docker build -f src/Quantumwake.OrgServer/Dockerfile -t quantumwake-orgserver .
# Run:
#   docker run -p 8080:8080 -v orgdata:/data \
#     -e OrgServer__PublicBaseUrl=https://org.example.net \
#     -e OrgServer__Discord__ClientId=... -e OrgServer__Discord__ClientSecret=... \
#     -e OrgServer__Admins=<discord-id> \
#     quantumwake-orgserver
# TLS terminates in front (a proxy, a tunnel, or App Service); set
# OrgServer__BehindProxy=true there so redirects and cookies know.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The props file carries the version; without it the image claims 1.0.0.
COPY Directory.Build.props /src/Directory.Build.props
COPY src/Quantumwake.OrgShared /src/src/Quantumwake.OrgShared
COPY src/Quantumwake.OrgServer /src/src/Quantumwake.OrgServer
RUN dotnet publish /src/src/Quantumwake.OrgServer -c Release -o /out

# Chiseled: no shell, no package manager, non-root. Microsoft.Data.Sqlite's
# bundled native library is glibc, which this image has.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
COPY --from=build /out /app
ENV OrgServer__Data=/data OrgServer__Bind=0.0.0.0 OrgServer__Port=8080
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "/app/Quantumwake.OrgServer.dll"]
