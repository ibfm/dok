# Guia de apresentação — Dok

Roteiro pra você se guiar durante a banca. Lê-se em ~5 minutos antes de começar; cada passo tem o que falar e o que executar.

---

## ⏱️ 0. Antes de começar (preparação 5 min antes da call)

```bash
cd /home/iberefm/ibfm/dok

# 1) Garantir que está verde
dotnet test
# Esperado: 53/53 passing (Domain 39 + Application 7 + Integration 7)

# 2) Subir o ambiente Docker
docker compose up --build -d
# Esperado: 3 containers rodando (provider-a, provider-b, api)

# 3) Verificar health
curl -s -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234"}' | python3 -m json.tool
# Esperado: 200 com placa ABC1234, debitos[2], total_atualizado 2355.93
```

Se o curl retornar 200 com o payload esperado, **tá pronto**. Mantém o `docker compose logs -f api` aberto em uma janela paralela para mostrar logs ao vivo.

> Tip: tenha 3 terminais abertos durante a apresentação:
> - **Terminal 1**: comandos `curl` / `docker compose stop`
> - **Terminal 2**: `docker compose logs -f api` (logs estruturados ao vivo)
> - **Terminal 3**: editor de código com o repo aberto (caso precise mostrar arquivos)

### Comandos úteis de Docker durante a apresentação

```bash
# Ver logs do container da API ao vivo (Ctrl+C para sair)
docker compose logs -f api

# Ver últimas 100 linhas dos logs (sem ficar seguindo)
docker compose logs --tail 100 api

# Ver logs de todos os containers ao mesmo tempo
docker compose logs -f

# Abrir um shell dentro do container da API (caso precise inspecionar)
docker compose exec api sh
# (digite 'exit' para sair)

# Status dos containers
docker compose ps

# Reiniciar só a API (sem mexer nos providers)
docker compose restart api

# Parar/subir um provider específico (pra demo de fallback)
docker compose stop provider-a
docker compose start provider-a

# Derrubar tudo, mantendo as imagens em cache
docker compose down

# Derrubar tudo e remover imagens (limpeza total)
docker compose down -v --rmi local
```

---

## 🎙️ 1. Abertura — narrativa de 60 segundos

Frase de abertura sugerida:

> *"Esse é um serviço .NET 10 que consulta múltiplos provedores de débitos veiculares, normaliza os dados, calcula juros simples e simula formas de pagamento. Vou mostrar primeiro a arquitetura, depois rodar o happy path, simular falha de provedor com fallback ao vivo, mostrar os códigos de erro estruturados, e fechar com os testes. Tudo está documentado em ADRs separados em `docs/architecture/`."*

---

## 🏛️ 2. Arquitetura (3 minutos)

Abra o diagrama mental. Pontos a destacar:

- **Multi-projeto** com Dependency Rule enforçada pelo compilador (ADR-007).
- **Domain puro** — `Dok.Domain.csproj` com **zero `PackageReference`**: prova visual.
  ```bash
  cat src/Dok.Domain/Dok.Domain.csproj
  # mostrar que tem só TargetFramework — ZERO refs externas
  ```
- **Hexagonal pragmático** (ADR-004): ports/adapters onde paga aluguel, sem cerimônia de Clean Arch.
- **Value Objects** (`Plate`, `Money`) como SSOT de validação e formatação (ADR-006).
- **Resiliência** via `Microsoft.Extensions.Http.Resilience` (Polly v8 sob o capô) com circuit breaker **isolado por provider** (ADR-009).
- **Logs Serilog** com mascaramento automático de placa via `IDestructuringPolicy` (LGPD, ADR-010).
- **TimeProvider** do BCL .NET 8+, com `FakeTimeProvider` em testes (ADR-012).
- **`Program.cs` enxuto (~25 linhas)** — toda configuração isolada em **6 extension methods** dedicados em `src/Dok.Api/Extensions/`, usando a sintaxe nova de **extension members do C# 14**.

> *"Essas decisões estão todas documentadas em ADRs separados, com contexto, opções consideradas, tradeoffs, decisão e justificativa. Posso abrir qualquer um se quiser."*

---

## 🟢 3. Demo do happy path

```bash
curl -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234"}'
```

**Fala**:

> *"Cliente manda uma placa. A API valida via Value Object `Plate` (regex Mercosul/antigo), passa pra um Service que orquestra: busca débitos no provider chain, aplica regras de juros via Strategy pattern (uma classe por tipo), calcula totais, gera as opções de pagamento (TOTAL, SOMENTE_IPVA, SOMENTE_MULTA com PIX 5% off e cartão Price/PMT 1×/6×/12×), e responde."*

**Aponte no payload**:
- `placa` retornada igual à enviada.
- `debitos` com 2 itens (IPVA cap 20% → 1800.00; MULTA sem cap → 555.93).
- `resumo.total_atualizado: "2355.93"` — bate com o exemplo da spec.
- Valores monetários como **string** (não float) — exigência da spec, garantida pelo `Money.ToJsonString()`.
- `pagamentos.opcoes[]` com TOTAL e SOMENTE_<TIPO> (singular).

**No terminal de logs**, mostre:

```
Querying ProviderA for ABC****        ← placa mascarada (LGPD)
ProviderA returned 2 debts for ABC****
```

> *"Repare que a placa aparece mascarada nos logs — é LGPD aplicado por configuração do Serilog, impossível de esquecer porque é uma destructuring policy."*

---

## 🔁 4. Demo do fallback ao vivo (o ponto alto)

### Como visualizar qual provider respondeu

Cada response carrega o header **`X-Dok-Provider`** indicando qual provider serviu os dados. Isso é visível em:

- **No Scalar UI**: ao expandir a resposta, na aba "Headers" — você vê `X-Dok-Provider: ProviderA` (ou `ProviderB`).
- **Via `curl -i`** (com `-i` mostra headers): `X-Dok-Provider: ProviderB`.
- **Nos logs do container** (terminal paralelo).

> ℹ️ O **body** da resposta permanece **literal conforme a spec** (sem campos extras). O `X-Dok-Provider` é metadado HTTP — header, não payload — então não viola o contrato. Argumento de banca: *"observability metadata em header; body permanece estritamente conforme a spec."*

### Roteiro do demo

**Passo 1** — Mostre o header com Provider A respondendo:

```bash
curl -i -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234"}' | head -20
```

> Aponte: `X-Dok-Provider: ProviderA` no topo da resposta.

**Passo 2** — Derrube o Provider A e refaça:

```bash
# Derruba o Provider A
docker compose stop provider-a

# Refaz a request
curl -i -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234"}' | head -20
```

> Aponte: agora aparece `X-Dok-Provider: ProviderB`. Mesma resposta funcional, provider diferente.

**Fala**:

> *"Provedor A foi derrubado. A request continua respondendo 200 normalmente — o `DebtProviderChain` detecta a falha de A, registra um warning estruturado, e cai pro provedor B (que retorna XML em vez de JSON). O adapter B parseia o XML e devolve o mesmo modelo canônico. O header `X-Dok-Provider` mostra explicitamente qual respondeu. Provider A está com circuit breaker próprio: se voltar ao normal, ele volta a ser tentado primeiro automaticamente."*

**Mostre nos logs do api** (no terminal paralelo `docker compose logs -f api`):

```
Querying ProviderA for ABC****
ProviderA failed for ABC**** — trying next provider
Querying ProviderB for ABC****
ProviderB returned 2 debts for ABC****
```

**Antes de seguir, suba o A de volta**:

```bash
docker compose start provider-a
```

---

## ❌ 5. Códigos de erro estruturados

A spec exige payloads literais. Demonstre cada um:

### 400 — placa inválida

```bash
curl -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"123"}'
```
Esperado:
```json
{"error":"invalid_plate"}
```

> *"Validação acontece no construtor do Value Object `Plate.Parse`. SSOT — uma fonte única de verdade. Não há `[Required]` ou regex duplicada em nenhum outro lugar."*

### 400 — campo desconhecido

```bash
curl -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234","extra":"x"}'
```
Esperado:
```json
{"error":"invalid_request"}
```

> *"Configurei `JsonUnmappedMemberHandling.Disallow` — o cliente que mandar typo (`placca` em vez de `placa`) recebe 400 imediato em vez de a API processar com placa nula silenciosamente."*

### 503 — todos os provedores indisponíveis

```bash
docker compose stop provider-a provider-b
curl -X POST http://localhost:8080/api/v1/debitos \
     -H 'Content-Type: application/json' \
     -d '{"placa":"ABC1234"}'
```
Esperado:
```json
{"error":"all_providers_unavailable"}
```

**Reerguer os providers depois**:

```bash
docker compose start provider-a provider-b
```

### 422 — tipo de débito desconhecido

Esse é mais difícil de demonstrar ao vivo (precisaria de um WireMock customizado). Argumente que **está coberto pelos testes de integração** — abra o arquivo:

```bash
sed -n '69,82p' tests/Dok.Integration.Tests/DebtsApiTests.cs
```

> *"O cenário está testado: provider retorna `LICENCIAMENTO`, API responde 422 com payload `{\"error\":\"unknown_debt_type\",\"type\":\"LICENCIAMENTO\"}` — exatamente o formato da spec."*

---

## 🧪 6. Cobertura de testes

```bash
dotnet test --logger "console;verbosity=normal" 2>&1 | grep -E "Passed!"
```

Esperado:
```
Passed! Domain.Tests        39/39
Passed! Application.Tests    7/7
Passed! Integration.Tests    7/7
```

> *"53 testes ao todo. Domain testes são unitários puros (Plate, Money HALF_UP, regras de juros com casos exatos da spec — IPVA 121 dias = 1800, MULTA 85 dias = 555.93). Application testa orquestração com NSubstitute. Integration tests sobem a API real com `WebApplicationFactory` e dois `WireMockServer` em portas dinâmicas — exercitam HTTP, Polly, JsonConverters, IExceptionHandlers de verdade."*

> *"Escolhi Shouldly em vez de FluentAssertions porque FA virou comercial em 2024. NSubstitute em vez de Moq pela polêmica do SponsorLink. Tudo defendido nos ADRs 013."*

---

## 📋 7. Documentação da API (Scalar UI)

Abra no browser:

```
http://localhost:8080/scalar
```

> *"OpenAPI gerado pelo `Microsoft.AspNetCore.OpenApi` nativo do .NET 9+ — pacote first-party da Microsoft. UI moderna pela Scalar (lib externa MIT). Note como os Value Objects `Plate` e `Money` aparecem com pattern e exemplo no schema — clientes que consomem a spec sabem o formato exato esperado."*

A spec JSON crua também está em `http://localhost:8080/openapi/v1.json`.

---

## ⚙️ 8. Configurabilidade — pra encerrar

> *"Tudo é configurável via `appsettings.json` ou variáveis de ambiente, sem rebuild. Por exemplo, posso aumentar o limite de body de 1 MiB pra 2 MiB editando `RequestLimits:MaxBodyBytes` ou setando `RequestLimits__MaxBodyBytes` no docker-compose. As únicas configurações com hot reload são as do Serilog; pra todas as outras, restart é necessário — limitação do framework, documentada no README."*

Mostre o snippet:
```bash
grep -A 5 "RequestLimits" src/Dok.Api/appsettings.json
```

---

## 🤖 9. Modificação ao vivo com IA (caso a banca peça)

**Cenários prováveis**:

### "Adicione um Provider C"
1. Criar `src/Dok.Infrastructure/Providers/ProviderCAdapter.cs` (copiar A ou B como base, ajustar parsing).
2. Registrar em `Dok.Infrastructure/DependencyInjection.cs`:
   ```csharp
   services.AddHttpClient<ProviderCAdapter>().ConfigureHttpClient(...).AddStandardResilienceHandler(...);
   services.AddTransient<IDebtProvider>(sp => sp.GetRequiredService<ProviderCAdapter>());
   ```
3. Adicionar `ProviderCUrl` em `appsettings.json` e em `ProvidersOptions`.
4. Subir um terceiro fake (ou ajustar `Dok.FakeProviders`).

### "Adicione tipo de débito LICENCIAMENTO"
1. Adicionar valor ao enum em `src/Dok.Domain/DebtType.cs`.
2. Atualizar `DebtTypeMapper.Parse` e `ToWire`.
3. Criar `src/Dok.Domain/Rules/LicenciamentoInterestRule.cs` implementando `IInterestRule`.
4. Registrar em `Dok.Application/DependencyInjection.cs`:
   ```csharp
   services.AddSingleton<IInterestRule, LicenciamentoInterestRule>();
   ```
5. Adicionar testes em `Dok.Domain.Tests`.

### "Mude a taxa de juros do IPVA"
- Ajustar `DailyRate` em `src/Dok.Domain/Rules/IpvaInterestRule.cs`.
- (Bônus: tornar configurável movendo pra `IOptions<InterestRulesOptions>` no domain.)

> *"A arquitetura foi pensada pra esse tipo de modificação: cada extensão acontece em um arquivo novo, sem tocar em código existente. Strategy + Adapter."*

---

## 🧯 10. Troubleshooting durante a banca

| Sintoma | Causa provável | Como resolver |
|---|---|---|
| `curl: (7) Failed to connect to localhost:8080` | Containers não estão rodando | `docker compose ps` para checar; `docker compose up -d` pra subir |
| Build falha com erro de pacote | NuGet desatualizado | `dotnet nuget locals all --clear && dotnet restore` |
| Porta 8080/9001/9002 já ocupada | Outro processo usando | `lsof -i :8080` (ou outras) e matar; ou ajustar `docker-compose.yml` |
| `dotnet test` quebrou em integração após mudar código | Cache estranho | `find . -type d \( -name bin -o -name obj \) \| xargs rm -rf && dotnet build` |
| Logs do Serilog não aparecem | Formato JSON em produção dificulta leitura | `ASPNETCORE_ENVIRONMENT=Development docker compose up` (mostra pretty) |

---

## 🎯 11. Pontos de defesa preparados

Argumentos guardados na manga, caso a banca questione:

| Tópico | Resposta curta |
|---|---|
| Por que .NET 10 e não 8? | Spec dá tempo de 7 dias; .NET 8 sai de suporte em ~7 meses. .NET 10 é LTS atual (suporte até nov/2028). |
| Por que Controllers e não Minimal API? | Familiaridade do ecossistema corporativo + filtros nativos + integração natural com `[ProducesResponseType]` no OpenAPI. |
| Por que Hexagonal pragmático e não Clean Arch puro? | 1 endpoint, 1 caso de uso, 0 persistência. Cerimônia de Clean Arch (UseCase classes, MediatR, Repository) não pagaria aluguel. |
| Por que multi-projeto? | Dependency Rule enforçada pelo **compilador**, não por convenção. `Dok.Domain.csproj` com 0 refs é prova visual de domínio puro. |
| Por que `TimeProvider` e não `IClock` custom? | `TimeProvider` é canônico no BCL desde .NET 8. Reinventar com `IClock` em 2026 seria NIH. `FakeTimeProvider` é oficial. |
| Por que Shouldly e não FluentAssertions? | FA virou comercial em jul/2024. Shouldly tem 15+ anos sob Apache/MIT. |
| Por que NSubstitute e não Moq? | Moq teve a polêmica SponsorLink (ago/2023). NSubstitute evita o drama. |
| Por que payload literal e não RFC 7807 ProblemDetails? | Spec dita formato literal. Conformidade com a spec prevalece. RFC 7807 fica como melhoria futura. |
| Interpretação estrita do 422 — por quê? | Texto 1 da spec é a regra geral (qualquer tipo desconhecido lança); texto 2 é caso particular. Silenciar tipos desconhecidos viola "Não silenciar". |
| Como você lida com divergência entre A e B? | A spec define fallback **sequencial**. Só um provider responde por request. Divergência observável não é parte do fluxo definido. |
| Por que `extension(...) { }` em vez de `this T param`? | Sintaxe de **extension members do C# 14** (.NET 10). Agrupa membros sobre o mesmo tipo num bloco e habilita **propriedades + operadores** de extensão (que `this T` nunca permitiu). Para os startup helpers atuais o ganho é estético; mas mostra que estou na sintaxe atual da linguagem e abre porta para extensões mais ricas no domínio. |
| Como o cliente sabe qual provider respondeu? | Header `X-Dok-Provider` na response (visível no Scalar UI). Body permanece literal conforme a spec — header é metadado HTTP, não payload. State holder `ProviderUsage` (Scoped) carrega o nome via DI; middleware lê e adiciona o header via `Response.OnStarting`. |

---

## 📚 12. Material de apoio (caso a banca queira mergulhar)

| Quero ver | Onde |
|---|---|
| Decisões arquiteturais detalhadas | `docs/architecture/` (18 ADRs) |
| Plano de implementação | `docs/PLANO-IMPLEMENTACAO.md` |
| Spec original | `docs/HomeTest-2.pdf` |
| Como rodar | `README.md` (3 caminhos: dev local, Docker, testes) |
| Decisões de divergência da spec | `README.md` → "Decisões interpretativas" |

---

## ✅ Checklist final (5 min antes da banca)

- [ ] `dotnet test` → 53/53 verde
- [ ] `docker compose up -d` → 3 containers up
- [ ] `curl http://localhost:8080/api/v1/debitos` → 200 com payload da spec
- [ ] Browser aberto em `http://localhost:8080/scalar`
- [ ] 3 terminais arrumados (curl / logs / editor)
- [ ] Cabeça respirando — você sabe defender cada decisão. Boa! 🚀
