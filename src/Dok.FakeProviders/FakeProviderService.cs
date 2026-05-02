using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace Dok.FakeProviders;

public sealed class FakeProviderService(
    IConfiguration config,
    ILogger<FakeProviderService> logger) : IHostedService
{
    private WireMockServer? _server;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = int.Parse(config["Provider:Port"] ?? "9001");
        var dataFile = config["Provider:DataFile"] ?? "data/providerA.json";
        var contentType = config["Provider:ContentType"] ?? "application/json";
        var name = config["Provider:Name"] ?? "Provider";
        // Demo-only: placa que dispara unknown_debt_type (tipo DPVAT) — útil pra mostrar 422 ao vivo.
        var unknownPlate = config["Provider:UnknownTypePlate"] ?? "UNK0000";

        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Port = port,
            StartAdminInterface = false,
        });

        var resolvedPath = Path.IsPathRooted(dataFile)
            ? dataFile
            : Path.Combine(AppContext.BaseDirectory, dataFile);

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Data file not found: {resolvedPath}");

        var body = File.ReadAllText(resolvedPath);
        var unknownBody = BuildUnknownDebtTypeBody(contentType, unknownPlate);

        // Regra específica (placa de demo): match exato. AtPriority menor = maior prioridade no WireMock.
        _server
            .Given(Request.Create()
                .WithPath($"/debts/{unknownPlate}")
                .UsingGet())
            .AtPriority(1)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", contentType)
                .WithBody(unknownBody));

        // Regra catch-all (qualquer placa válida): retorna o data file normal.
        _server
            .Given(Request.Create()
                .WithPath(new RegexMatcher("^/debts/[A-Z0-9]+$"))
                .UsingGet())
            .AtPriority(10)
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", contentType)
                .WithBody(body));

        logger.LogInformation(
            "{Name} listening on {Url} (data: {DataFile}, content-type: {ContentType}, demo plate {UnknownPlate} → DPVAT)",
            name, _server.Url, dataFile, contentType, unknownPlate);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _server?.Stop();
        _server?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gera o payload de "tipo desconhecido" no formato anunciado pelo provider.
    /// O tipo retornado é <c>DPVAT</c> — não está mapeado em <c>DebtTypeMapper</c>,
    /// então dispara <c>UnknownDebtTypeException</c> → HTTP 422 conforme spec.
    /// </summary>
    private static string BuildUnknownDebtTypeBody(string contentType, string plate)
    {
        var isXml = contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);
        if (isXml)
        {
            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <response>
                  <plate>{plate}</plate>
                  <debts>
                    <debt><category>DPVAT</category><value>150.00</value><expiration>2024-01-10</expiration></debt>
                  </debts>
                </response>
                """;
        }
        return $$"""
            {
              "vehicle": "{{plate}}",
              "debts": [
                { "type": "DPVAT", "amount": 150.00, "due_date": "2024-01-10" }
              ]
            }
            """;
    }
}
