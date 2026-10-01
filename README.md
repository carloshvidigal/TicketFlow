# TicketFlow

Plataforma de venda e reserva de ingressos. Projeto de portfólio full-stack
(.NET + Angular), com foco declarado em consistência de dados, concorrência,
segurança e arquitetura modular — não em empilhar tecnologia.

> **Status:** em desenvolvimento inicial (Fase 1 do roadmap). O que está
> descrito abaixo reflete o que já existe no repositório, não o produto final.

## Motivação

O desafio técnico central do projeto é garantir a disponibilidade correta de
ingressos sob concorrência: nunca vender o mesmo ingresso duas vezes, nunca
deixar o estoque ficar negativo, e tratar de forma explícita reservas,
pagamentos simulados e idempotência.

## Arquitetura

Solution .NET organizada em camadas, sem pastas `src/` ou `Modules/` na raiz:

```
TicketFlow.slnx
├── TicketFlow.Api/             (ASP.NET Core — Controllers, composição)
├── TicketFlow.Application/     (casos de uso / regras de aplicação)
├── TicketFlow.Domain/          (entidades e regras de negócio)
│   ├── Events/                 (Event, Section)
│   └── Tickets/                (Ticket)
├── TicketFlow.Infrastructure/  (EF Core, Npgsql, acesso a dados)
├── TicketFlow.UnitTests/
├── TicketFlow.IntegrationTests/
└── frontend/                   (Angular — ainda não iniciado)
```

Módulos de domínio adicionais (Auth, Users, Reservations, Orders, Payments,
Notifications) vão existir como pastas dentro desses mesmos projetos, à
medida que forem implementados — não como projetos próprios.

Fluxo de dependências: `Api → Application → Domain`. A `Application` define
contratos (ex.: `IPasswordHasher`, `IUserRepository`) e a `Infrastructure`
os implementa (Argon2, EF Core) — a `Api` só faz a composição.

## Stack

| Área              | Escolha                              |
|--------------------|--------------------------------------|
| Runtime            | .NET 10 (LTS)                        |
| HTTP               | ASP.NET Core (Controllers)           |
| Banco              | PostgreSQL                           |
| ORM                | Entity Framework Core (Npgsql)       |
| Validação          | FluentValidation                     |
| Testes             | xUnit + Testcontainers               |
| Frontend           | Angular (ainda não iniciado)         |
| Containerização    | Docker + Docker Compose              |
| CI                 | GitHub Actions                       |

A lista completa de decisões (e as que ainda estão em aberto) está no
documento de arquitetura do projeto.

## Principais desafios

- **Concorrência:** duas compras simultâneas do último ingresso não podem
  gerar duas vendas.
- **Reservas temporárias:** um ingresso reservado expira e volta a ficar
  disponível se o pagamento não for concluído a tempo.
- **Pagamentos:** simulados via um provider fake, para estudar problemas reais
  de integração externa (timeout, recusa, webhook duplicado).
- **Idempotência:** operações críticas (criar pedido, confirmar pagamento,
  processar webhook) não podem produzir efeitos duplicados quando repetidas.

A estratégia exata de locking, isolamento de transação e expiração de
reservas ainda é uma decisão em aberto — será resolvida quando esse trecho do
domínio for implementado (Fase 4 do roadmap).

## Como executar

Pré-requisito: Docker.

```bash
cp .env.example .env
# preencha JWT_SECRET no .env com um valor aleatório (mínimo 32 bytes):
openssl rand -base64 48
docker compose up --build
```

A API sobe em `http://localhost:8080` (porta configurável via `.env`). Sem um
`JWT_SECRET` válido ela **não sobe**: o segredo que assina os tokens vem
sempre de variável de ambiente e nunca é versionado.

## Como testar

```bash
# testes unitários (domínio, sem dependências externas)
dotnet test TicketFlow.UnitTests

# testes de integração (sobem um PostgreSQL real via Testcontainers — requer Docker)
dotnet test TicketFlow.IntegrationTests

# checagem de formatação, igual ao que roda na CI
dotnet format TicketFlow.slnx --verify-no-changes
```

## API

Endpoints disponíveis (exemplos em [`TicketFlow.Api.http`](TicketFlow.Api/TicketFlow.Api.http)):

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/auth/register` | Cria um usuário `Customer` (`201`; `400` dados inválidos; `409` e-mail já cadastrado) |
| `POST` | `/auth/login` | Devolve access token (JWT, 15 min) e refresh token (7 dias) (`200`; `401` credenciais inválidas) |
| `POST` | `/auth/refresh` | Troca o refresh token por um novo par; o antigo deixa de valer (`200`; `401` token inválido, expirado ou já usado) |
| `POST` | `/auth/logout` | Revoga o refresh token (`204`, sempre) |

Todos os erros seguem o mesmo formato:

```json
{ "error": { "code": "EMAIL_ALREADY_REGISTERED", "message": "The email is already registered." } }
```

Erros de validação incluem também `details` com o campo de cada problema. A
documentação OpenAPI será adicionada mais adiante (Fase 8 do roadmap).

## Decisões

Decisões arquiteturais relevantes são registradas como Architecture Decision
Records (ADRs) em [`docs/adr/`](docs/adr/README.md).

## Roadmap

Ver documento de arquitetura do projeto para o roadmap técnico completo
(Fases 1 a 10).

**Fase 1 — Fundamentos:** concluída.
- [x] Solution .NET em camadas
- [x] PostgreSQL + Entity Framework Core + migrations
- [x] Docker Compose (api + postgres), com migrations aplicadas
      automaticamente no boot em Development
- [x] CI (build, format, testes)

**Modelagem de domínio** (adiantada em relação ao roadmap, antes de entrar
fundo nas Fases 2–6): `Event`, `Section`, `Ticket`, `User`, `Reservation`,
`Order`, `Payment` já existem com suas regras de ciclo de vida e cobertura de
testes. Ainda faltam, por fase:

- [x] Hash de senha (Argon2id), tratamento centralizado de erros e registro
      de usuário (`POST /auth/register`) — Fase 2, etapas 1 e 2
- [x] Login, JWT (access token de 15 min) e refresh token rotativo com
      detecção de reuso (`/auth/login`, `/auth/refresh`, `/auth/logout`) —
      Fase 2, etapa 3
- [ ] Validação do JWT nas rotas, autorização por papel e `GET /me` —
      Fase 2, etapa 4
- [ ] Endpoints da Api (Controllers) para eventos e setores (Fase 3)
- [ ] Orquestração de reserva → pedido → pagamento na Application layer
      (Fases 4–6)
- [ ] Frontend Angular (Fase 7)
