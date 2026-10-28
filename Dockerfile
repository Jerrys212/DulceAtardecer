FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DulceAtardecer.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM build AS bundle
ARG DOTNET_EF_VERSION=10.0.14
RUN dotnet tool install --global dotnet-ef --version ${DOTNET_EF_VERSION}
ENV PATH="${PATH}:/root/.dotnet/tools"
# EF tooling runs Program.cs, which rejects a missing JWT key; this one exists only at build time.
RUN ApiSettings__SecretKey=build-only-placeholder-key-not-used-at-runtime \
    dotnet ef migrations bundle --configuration Release -o /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /app
COPY --from=bundle /app/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DulceAtardecer.dll"]
