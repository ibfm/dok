# Guia de apresentação — Dok

Roteiro pra você se guiar durante a banca. Lê-se em ~5 minutos antes de começar; cada passo tem o que falar e o que executar.

---

## ⏱️ 0. Antes de começar (preparação 5 min antes da call)

```bash
cd /home/iberefm/ibfm/dok

# 1) Garantir que está verde
dotnet test
# Esperado: 57/57 passing (Domain 39 + Application 7 + Integration 11)

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
Passed! Integration.Tests   11/11
```

> *"57 testes ao todo. Domain testes são unitários puros (Plate, Money HALF_UP, regras de juros com casos exatos da spec — IPVA 121 dias = 1800, MULTA 85 dias = 555.93). Application testa orquestração com NSubstitute. Integration tests sobem a API real com `WebApplicationFactory` e dois `WireMockServer` em portas dinâmicas — exercitam HTTP, Polly, JsonConverters, IExceptionHandlers de verdade. Cobrem happy path, fallback A→B, 503, 422, 400 (placa inválida + JSON malformado + campo desconhecido), `<debts/>` autofechado, débito futuro com juros zerado, e múltiplos débitos do mesmo tipo agrupados em SOMENTE_<TIPO> singular."*

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

> **Decisão arquitetural**: os 3 cenários esperados estão empacotados como **skills do Claude Code** versionadas no repo (`.claude/skills/`). Cada skill abre uma feature branch a partir de `main` atualizado, faz as edições, valida com `dotnet build` + testes, dá commit, push e abre um **PR no GitHub** via `gh pr create`. URL do PR é o output final visível pra banca. Justificativa completa em [ADR-019](architecture/ADR-019-Skills-Modificacao-Ao-Vivo.md).

**Frase de transição para a banca**:

> *"Os três cenários típicos de extensão estão pré-empacotados como skills do Claude Code. Eu disparo uma slash-command, respondo as perguntas, e a skill abre uma feature branch a partir da main, faz as edições, valida com build+test, e termina abrindo um PR no GitHub. No fim eu mando o link pra vocês revisarem o diff. É prompt-as-code: versionado no repo, ensaiado antes da call. Se vocês pedirem algo fora desses três, eu disparo ad-hoc."*

### Cenários cobertos por skill

| Skill | Pergunta | Branch | Faz |
|---|---|---|---|
| `/add-provider` | Nome (`C`/`D`/...), URL base, formato (`JSON`/`XML`) | `feat/add-provider-<x>` | Cria `Provider<X>Adapter.cs`, registra DI + Polly, atualiza `ProvidersOptions` + `appsettings.json` + `docker-compose.yml`, ajusta `Dok.FakeProviders`. PR no fim. |
| `/add-debt-type` | Nome do tipo, taxa diária (%), cap opcional (%) | `feat/add-debt-type-<tipo>` | Adiciona enum, atualiza `DebtTypeMapper`, cria `<X>InterestRule`, registra no DI da Application, gera testes em `Dok.Domain.Tests`. PR no fim. |
| `/change-interest-rate` | Tipo (`ipva`/`multa`), nova taxa diária ou cap | `feat/change-interest-rate-<tipo>` | Edita a constante na rule, ajusta os testes que dependem da constante. PR no fim. |

### Fluxo na call

1. **Banca pede**: *"adiciona um Provider C que retorna JSON em `http://localhost:9003`"*.
2. Você digita `/add-provider` no Claude Code (terminal 3, com o repo aberto).
3. A skill pergunta nome, URL e formato — você responde nas opções estruturadas.
4. A skill checa working tree limpo → atualiza `main` → cria `feat/add-provider-c` → faz as edições → roda `dotnet build` + `dotnet test` → commita → push → `gh pr create`.
5. A skill imprime a URL do PR. Você copia, cola no chat com a banca: *"PR aberto, link aqui"*.
6. Opcional: refazer o `curl` mostrando o novo provider na chain (precisa rebuild do container, então só se houver tempo).
7. Se a banca aprovar, `gh pr merge`. Se não, `gh pr close` + `git checkout main` mantém tudo limpo.

### Guardrails

- **Pré-flight**: working tree precisa estar limpo. `main` é puxado com `--ff-only` antes do checkout da feature branch.
- **Branch nomeada por convenção**: `feat/<skill>-<param>` (ex: `feat/add-provider-c`). Nunca toca `main` direto.
- **Validação obrigatória antes do commit**: `dotnet build` + `dotnet test` (escopo afetado). Falha aborta sem commitar nem abrir PR.
- **`git add` explícito**: skills listam os arquivos que adicionaram/editaram; nunca `git add -A` (evita acidente de commitar arquivo solto).
- **Escopo de arquivos restrito**: skills **não tocam** em `Directory.Build.props`, `Dok.slnx`, `Dockerfile`, `Makefile`, `.github/`, `docs/architecture/`, ou `.claude/` (ADR-019 sub-decisão 5).
- **Ensaiadas 3× antes da banca** em branches descartáveis. PRs do ensaio fechados sem merge.

### Fallback manual (caso uma skill quebre ao vivo)

Os passos manuais cobertos pelas skills, caso seja necessário executar à mão:

#### "Adicione um Provider C" (manual)
1. Criar `src/Dok.Infrastructure/Providers/ProviderCAdapter.cs` (copiar A ou B como base, ajustar parsing).
2. Registrar em `Dok.Infrastructure/DependencyInjection.cs`:
   ```csharp
   services.AddHttpClient<ProviderCAdapter>().ConfigureHttpClient(...).AddStandardResilienceHandler(...);
   services.AddTransient<IDebtProvider>(sp => sp.GetRequiredService<ProviderCAdapter>());
   ```
3. Adicionar `ProviderCUrl` em `appsettings.json` e em `ProvidersOptions`.
4. Subir um terceiro fake (ou ajustar `Dok.FakeProviders`).

#### "Adicione tipo de débito LICENCIAMENTO" (manual)
1. Adicionar valor ao enum em `src/Dok.Domain/DebtType.cs`.
2. Atualizar `DebtTypeMapper.Parse` e `ToWire`.
3. Criar `src/Dok.Domain/Rules/LicenciamentoInterestRule.cs` implementando `IInterestRule`.
4. Registrar em `Dok.Application/DependencyInjection.cs`:
   ```csharp
   services.AddSingleton<IInterestRule, LicenciamentoInterestRule>();
   ```
5. Adicionar testes em `Dok.Domain.Tests`.

#### "Mude a taxa de juros do IPVA" (manual)
- Ajustar `DailyRate` em `src/Dok.Domain/Rules/IpvaInterestRule.cs`.
- (Bônus: tornar configurável movendo pra `IOptions<InterestRulesOptions>` no domain.)

> *"A arquitetura foi pensada pra esse tipo de modificação: cada extensão acontece em um arquivo novo, sem tocar em código existente. Strategy + Adapter. As skills só conseguem ser determinísticas porque os pontos de extensão já estavam preparados pelos ADRs anteriores."*

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
| Por que empacotar a modificação ao vivo como skill em vez de prompt ad-hoc? | Determinismo sob pressão de palco e prompt-as-code: a skill é versionada (`.claude/skills/`), ensaiada antes da call, isolada num branch novo, validada por build+test. Resto continua ad-hoc. ADR-019. |
| As skills "trapaceiam" mostrando IA fazendo o que já estava roteirizado? | Não — elas demonstram engenharia em torno do uso de IA (versionamento, guardrails, ensaio). Para mudança fora do escopo das 3 skills, o ad-hoc continua disponível e a banca pode pedir qualquer coisa. |

---

## 📚 12. Material de apoio (caso a banca queira mergulhar)

| Quero ver | Onde |
|---|---|
| Decisões arquiteturais detalhadas | `docs/architecture/` (19 ADRs) |
| Plano de implementação | `docs/PLANO-IMPLEMENTACAO.md` |
| Spec original | `docs/HomeTest-2.pdf` |
| Como rodar | `README.md` (3 caminhos: dev local, Docker, testes) |
| Decisões de divergência da spec | `README.md` → "Decisões interpretativas" |
| Skills de modificação ao vivo (item 9) | `.claude/skills/` + ADR-019 |

---

## ✅ Checklist final (5 min antes da banca)

- [ ] `dotnet test` → 57/57 verde
- [ ] `docker compose up -d` → 3 containers up
- [ ] `curl http://localhost:8080/api/v1/debitos` → 200 com payload da spec
- [ ] Browser aberto em `http://localhost:8080/scalar`
- [ ] 3 terminais arrumados (curl / logs / editor)
- [ ] Skills do item 9 ensaiadas 3× em branches descartáveis (ADR-019)
- [ ] Working tree limpo em `main` (skills exigem isso pra criar branch novo)
- [ ] Cabeça respirando — você sabe defender cada decisão. Boa! 🚀
