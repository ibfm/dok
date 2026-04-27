# ADR-003 — Estilo de API: Minimal API vs Controllers

**Status:** aceito
**Data:** 2026-04-27

## Contexto

ASP.NET Core oferece dois estilos para expor endpoints HTTP:

- **Minimal API** (introduzido em .NET 6, refinado em 7/8/9/10): endpoints declarados diretamente em `Program.cs` (ou módulos de extensão), sem necessidade de classes de Controller; bind de parâmetros via convenção; ideal para APIs pequenas/médias e microsserviços.
- **Controllers (MVC)**: classes que herdam de `ControllerBase`, atributos `[HttpGet]`/`[HttpPost]`, model binding clássico, filtros, ações; padrão histórico do ASP.NET, ainda muito presente em sistemas corporativos.

A escolha afeta: estrutura do código de borda, ergonomia de versionamento, integração com Swagger, posicionamento dos validadores, e a percepção da banca ("moderno e enxuto" vs "tradicional e familiar").

## Opções consideradas

| Estilo         | Prós                                                                                              | Contras                                                                                          |
|----------------|----------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| **Minimal API**| Menos boilerplate; fácil mostrar fluxo HTTP de cabo a rabo na apresentação; alinhado a microsserviços modernos; integra bem com `TypedResults` e OpenAPI nativo do .NET 10 | Pode parecer "informal" para avaliadores conservadores; agrupar muitos endpoints em um único `Program.cs` exige disciplina (módulos de extensão) |
| **Controllers**| Familiaridade ampla na comunidade .NET; filtros (`[Authorize]`, `[ValidateAntiForgery]`) prontos; estrutura por convenção facilita projetos grandes | Mais cerimônia (classes, atributos, herança); para API com 1-2 endpoints é overkill; sinaliza "default histórico" e não "escolha consciente" |

## Tradeoffs principais

- **Tamanho do projeto**: o desafio tem tipicamente **um único endpoint** (`POST /api/v1/debitos`). Para isso, Controllers é overkill — uma classe inteira para um método.
- **Apresentação**: Minimal API permite mostrar todo o fluxo HTTP em poucas linhas, idealmente em um arquivo `DebtsEndpoints.cs` que importa o use case e mapeia o endpoint. Isso fica visualmente impactante na banca.
- **Extensibilidade**: ambos suportam DI, middleware, validação, filtros (Minimal API ganhou `AddEndpointFilter` em .NET 7+). Não há limitação prática para o escopo deste desafio.
- **Mensagem de senioridade**: escolher Minimal API com "é o estilo recomendado pela Microsoft para APIs pequenas, e o desafio cabe nele" é mais forte que "usei Controllers porque é o que sempre uso".

## Recomendação preliminar

**Minimal API**, organizada em um arquivo de extensão (`DebtsEndpoints.cs`) que mantém `Program.cs` limpo. Isso casa com a escala do projeto e com a mensagem moderna que queremos passar.

## Decisão

**Controllers (MVC)**.

## Justificativa

1. Familiaridade do candidato e da maior parte do mercado .NET corporativo brasileiro — facilita defesa na banca sem precisar gastar tempo justificando o estilo em si.
2. Controllers oferecem filtros (`ActionFilter`, `ExceptionFilter`), `ApiController` attribute (com validação automática e respostas 400 padronizadas), e `ProblemDetails` integrados de forma natural — ergonomia útil para os requisitos 400/422/503 do enunciado.
3. O leve overhead de boilerplate é aceitável dado que o ganho em estrutura visível ("isso é a Controller, isso é o Service") ajuda na narrativa de apresentação ao vivo.

## Notas

- Mesmo com Controllers, manter o `Program.cs` enxuto, com configuração isolada em métodos de extensão (`AddDebtsModule`, `UseDebtsModule`) para não virar `God file`.

---
