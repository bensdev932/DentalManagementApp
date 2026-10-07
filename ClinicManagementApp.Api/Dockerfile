# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["ClinicManagementApp.Api.csproj", "./"]
RUN dotnet restore "ClinicManagementApp.Api.csproj"

# Copy full source and publish release build
COPY . .
RUN dotnet publish "ClinicManagementApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Low-memory container optimizations (Render 512MB RAM constraint)
ENV DOTNET_gcServer=0
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimitPercent=60

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClinicManagementApp.Api.dll"]

