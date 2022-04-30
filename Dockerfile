FROM mcr.microsoft.com/dotnet/sdk:6.0 as builder
COPY /src /app
RUN dotnet publish -c Release -o /out /app/update-readme-action.csproj
COPY entrypoint.sh /out/entrypoint.sh

FROM mcr.microsoft.com/dotnet/runtime:6.0 as base
COPY --from=builder /out /app