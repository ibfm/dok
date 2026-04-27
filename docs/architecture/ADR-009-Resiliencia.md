# ADR-009 — Resiliência: Polly v8 / Microsoft.Extensions.Http.Resilience

**Status:** aceito
**Data:** 2026-04-27

## Contexto

O enunciado pede:

- Fallback caso um provedor falhe.
- Idealmente: simular timeout/indisponibilidade, retry com backoff, circuit breaker.

A decisão tem duas camadas:

1. **Lib de resiliência**: Polly v8 puro vs `Microsoft.Extensions.Http.Resilience` (que usa Polly v8 por baixo, com integração first-class ao `IHttpClientFactory`).
2. **Quais políticas, em que ordem, com que parâmetros**.

E um ponto importante: **resiliência intra-provider (Polly) é diferente de fallback inter-provider** — o fallback A → B é responsabilidade do `DebtProviderChain` (ADR-004), não do Polly. Polly cuida das tentativas e proteções **dentro** de um único provider.

## Camada 1 — Lib de resiliência

### Opção A — `Polly` v8 puro

Pacote `Polly` (≥ 8.x) com `ResiliencePipeline<T>` configurado manualmente, integrado via `AddHttpClient(...).AddPolicyHandler(...)` ou `AddResilienceHandler(...)`.

- ✅ Controle total: cada estratégia configurada explicitamente, fácil mostrar na banca a pipeline construída no código.
- ✅ Documentação rica e a própria lib é referência da indústria.
- ❌ Mais código boilerplate de configuração.

### Opção B — `Microsoft.Extensions.Http.Resilience` (recomendada)

Pacote da Microsoft que envolve Polly v8 com defaults sensatos. Configuração via `AddHttpClient(...).AddStandardResilienceHandler(opts => ...)`.

- ✅ **Microsoft-endorsed** — em apresentação sênior, *"usei o pacote oficial de resiliência da Microsoft, que internamente usa Polly v8"* é argumento forte.
- ✅ Defaults sensatos: timeout, retry com jitter, circuit breaker, rate limiter — tudo já configurado, só ajustar parâmetros.
- ✅ Integração nativa com `IHttpClientFactory`, telemetria, e `Microsoft.Extensions.Logging`.
- ✅ Em .NET 10, é o caminho idiomático.
- ❌ Um nível de abstração a mais — se quiser políticas exóticas, pode ter que descer pra Polly puro.
- ❌ Para a banca, é menos visível "o que está acontecendo" — defaults invisíveis.

## Camada 2 — Quais políticas e em que ordem

A pipeline canônica em sistemas HTTP segue (do mais externo para o mais interno):

```
[Total Timeout] → [Retry com backoff] → [Circuit Breaker] → [Per-Attempt Timeout] → [HTTP call]
```

- **Total Timeout** (outermost): garante que toda a operação termina em X segundos, mesmo com retries. Evita "morte lenta".
- **Retry**: tenta de novo em falhas transientes (5xx, timeout, connection refused). Backoff exponencial + jitter para não sobrecarregar o provedor que está se recuperando.
- **Circuit Breaker**: depois de N falhas em janela Y, "abre" e rejeita rapidamente novas chamadas por Z segundos — protege o provedor degradado e libera recursos do nosso lado.
- **Per-Attempt Timeout** (innermost): cada chamada HTTP tem seu próprio limite, separado do total.

### Parâmetros propostos (ajustáveis em `appsettings.json`)

| Política | Valor proposto | Justificativa |
|---|---|---|
| Total timeout | 10s | Cliente HTTP do desafio espera resposta síncrona — não pode esperar para sempre |
| Retry attempts | 2 (3 chamadas no total) | Falhas transientes geralmente passam em 1-2 retentativas; mais que isso é desperdício |
| Retry backoff | exponencial com jitter (200ms base, fator 2) | Padrão da indústria; jitter evita "thundering herd" |
| Circuit breaker | 5 falhas em 30s → aberto por 30s | Conservador; em produção real ajustaria por SLA do provedor |
| Per-attempt timeout | 3s | Se provedor não respondeu em 3s, é falha — partir para retry/fallback |

### Crucial: circuit breaker **por provider**

Cada `HttpClient` (Provider A, Provider B) registra seu **próprio** circuit breaker. Provider A degradado **não** deve afetar Provider B. Isso significa que o `DebtProviderChain` consegue, mesmo com A em circuito aberto, ir direto para B sem esperar timeout — exatamente o comportamento que defendemos como "fallback resiliente".

## Decisão

- **Lib**: `Microsoft.Extensions.Http.Resilience` (que usa Polly v8 internamente).
- **Pipeline canônica** (do mais externo para o mais interno): Total Timeout → Retry com backoff exponencial e jitter → Circuit Breaker → Per-Attempt Timeout → HTTP call.
- **Parâmetros iniciais** (configuráveis em `appsettings.json`):
  - Total timeout: 10s.
  - Retry: 2 tentativas, backoff base 200ms, fator 2, com jitter.
  - Circuit breaker: 5 falhas em 30s → aberto por 30s.
  - Per-attempt timeout: 3s.
- **Circuit breaker isolado por provider**: cada `HttpClient` nomeado (`"providerA"`, `"providerB"`) tem sua própria instância — degradação de A não afeta B.

## Justificativa

1. `Microsoft.Extensions.Http.Resilience` é o pacote oficial Microsoft para resiliência em .NET 8+, com Polly v8 por baixo. Usar o pacote endorsed em vez do Polly direto soma um argumento (oficialidade) sem perder expressividade.
2. A ordem da pipeline é canônica e protege contra todos os modos de falha: total timeout impede "morte lenta", retry trata transientes, circuit breaker protege provedor degradado e libera recursos do nosso lado, per-attempt timeout dá deadline a cada chamada individual.
3. Backoff exponencial com **jitter** evita thundering herd quando o provedor estiver se recuperando.
4. Circuit breaker por provider é o que torna o fallback do `DebtProviderChain` realmente resiliente: com A em circuito aberto, a chamada para A é rejeitada **imediatamente** (sem esperar timeout) e a chain segue para B sem latência adicional.
5. Parâmetros em `appsettings.json` permitem ajuste sem rebuild — argumento extra na banca de "operacionalmente saudável".

---
