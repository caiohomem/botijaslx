using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Botijas.PrintGateway;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<LabelGenerator>();
        services.AddHostedService<PrintGatewayWorker>();
    })
    .Build();

await host.RunAsync();

public class PrintGatewayWorker : BackgroundService
{
    private readonly ILogger<PrintGatewayWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly LabelGenerator _labelGenerator;
    private HubConnection? _connection;

    public PrintGatewayWorker(
        ILogger<PrintGatewayWorker> logger,
        IConfiguration configuration,
        LabelGenerator labelGenerator)
    {
        _logger = logger;
        _configuration = configuration;
        _labelGenerator = labelGenerator;
        
        InitializeLabelGenerator();
    }

    private void InitializeLabelGenerator()
    {
        // Configure label size (75x50mm)
        _labelGenerator.SetLabelSize(75, 50);
        
        // Load logo
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "lxbrewery.png");
        if (!File.Exists(logoPath))
        {
            // Try relative path
            logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "lxbrewery.png");
        }
        
        if (File.Exists(logoPath))
        {
            _labelGenerator.LoadLogo(logoPath, maxWidthMm: 35, maxHeightMm: 18);
            _logger.LogInformation("Logo loaded from {Path}", logoPath);
        }
        else
        {
            _logger.LogWarning("Logo not found at {Path}", logoPath);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubUrl = _configuration["PrintHub:Url"] ?? "http://localhost:5001/hubs/print";
        var printerIp = _configuration["Printer:IpAddress"] ?? "";
        var printerPort = int.Parse(_configuration["Printer:Port"] ?? "9100");
        var simulateMode = string.IsNullOrEmpty(printerIp);

        if (simulateMode)
        {
            _logger.LogWarning("No printer IP configured. Running in SIMULATION mode.");
            _logger.LogWarning("To configure printer, set Printer:IpAddress in appsettings.json");
        }
        else
        {
            _logger.LogInformation("Printer configured: {Ip}:{Port}", printerIp, printerPort);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Handle print job with customer data
        _connection.On<Guid, int, string?, string?, string?>("PrintJobCreated", 
            async (jobId, quantity, templateId, customerName, customerPhone) =>
        {
            _logger.LogInformation(
                "Print job received: {JobId}, Qty: {Qty}, Customer: {Customer}", 
                jobId, quantity, customerName ?? "N/A");

            try
            {
                // Generate labels
                var labels = new List<LabelData>();
                for (var i = 0; i < quantity; i++)
                {
                    labels.Add(new LabelData
                    {
                        QrContent = $"{jobId}-{i + 1}", // Unique QR per label
                        CustomerName = customerName,
                        CustomerPhone = customerPhone
                    });
                }

                var zpl = _labelGenerator.GenerateLabels(labels);
                _logger.LogDebug("Generated ZPL ({Bytes} bytes)", zpl.Length);

                bool success;
                if (simulateMode)
                {
                    // Simulate printing
                    _logger.LogInformation("SIMULATION: Would print {Qty} label(s)", quantity);
                    await Task.Delay(500, stoppingToken);
                    success = true;
                    
                    // Log ZPL for debugging
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("ZPL:\n{Zpl}", zpl);
                    }
                }
                else
                {
                    // Real print
                    var printer = new ZebraPrinter(printerIp, printerPort);
                    success = await printer.PrintAsync(zpl, stoppingToken);
                }

                if (success)
                {
                    await AcknowledgePrinted(jobId);
                }
                else
                {
                    await AcknowledgeFailed(jobId, "Failed to send to printer");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing print job {JobId}", jobId);
                await AcknowledgeFailed(jobId, ex.Message);
            }
        });

        // Legacy handler (without customer data) for backwards compatibility
        _connection.On<Guid, int, string?>("PrintJobCreatedLegacy", 
            async (jobId, quantity, templateId) =>
        {
            _logger.LogInformation("Legacy print job: {JobId}, Qty: {Qty}", jobId, quantity);
            
            // Simulate
            await Task.Delay(500, stoppingToken);
            await AcknowledgePrinted(jobId);
        });

        _connection.Reconnecting += error =>
        {
            _logger.LogWarning(error, "Reconnecting to SignalR hub...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected. Connection ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            _logger.LogError(error, "SignalR connection closed");
            return Task.CompletedTask;
        };

        // Connection loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogInformation("Connecting to {Url}...", hubUrl);
                    await _connection.StartAsync(stoppingToken);
                    _logger.LogInformation("Connected! Waiting for print jobs...");
                }

                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection error");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task AcknowledgePrinted(Guid jobId)
    {
        try
        {
            var apiUrl = _configuration["Api:Url"] ?? "http://localhost:5001";
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(
                $"{apiUrl}/api/print-jobs/{jobId}/ack-printed",
                null);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Job {JobId} acknowledged as printed", jobId);
            }
            else
            {
                _logger.LogWarning("Failed to ack job {JobId}: {Status}", jobId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging job {JobId}", jobId);
        }
    }

    private async Task AcknowledgeFailed(Guid jobId, string error)
    {
        try
        {
            var apiUrl = _configuration["Api:Url"] ?? "http://localhost:5001";
            using var httpClient = new HttpClient();
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new { error }), 
                System.Text.Encoding.UTF8, 
                "application/json");
            
            var response = await httpClient.PostAsync(
                $"{apiUrl}/api/print-jobs/{jobId}/ack-failed",
                content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Job {JobId} acknowledged as failed: {Error}", jobId, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging failed job {JobId}", jobId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
