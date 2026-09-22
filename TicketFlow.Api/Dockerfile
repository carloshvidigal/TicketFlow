# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TicketFlow.slnx .
COPY TicketFlow.Api/TicketFlow.Api.csproj TicketFlow.Api/
COPY TicketFlow.Application/TicketFlow.Application.csproj TicketFlow.Application/
COPY TicketFlow.Domain/TicketFlow.Domain.csproj TicketFlow.Domain/
COPY TicketFlow.Infrastructure/TicketFlow.Infrastructure.csproj TicketFlow.Infrastructure/
RUN dotnet restore TicketFlow.Api/TicketFlow.Api.csproj

COPY TicketFlow.Api/ TicketFlow.Api/
COPY TicketFlow.Application/ TicketFlow.Application/
COPY TicketFlow.Domain/ TicketFlow.Domain/
COPY TicketFlow.Infrastructure/ TicketFlow.Infrastructure/

RUN dotnet publish TicketFlow.Api/TicketFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TicketFlow.Api.dll"]
