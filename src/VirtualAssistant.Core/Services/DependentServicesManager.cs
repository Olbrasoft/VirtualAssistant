using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages lifecycle of dependent services (e.g., TextToSpeech.Service).
/// </summary>
public class DependentServicesManager : IDependentServiceManager
{
    private readonly ILogger<DependentServicesManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, DependentServiceInfo> _services = new();
    private readonly CancellationTokenSource _monitoringCts = new();
    private Task? _monitoringTask;
    private bool _disposed;

    public event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;

    public DependentServicesManager(
        ILogger<DependentServicesManager> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        // Register dependent services
        _services.Add("TextToSpeech.Service", new DependentServiceInfo
        {
            Name = "TextToSpeech.Service",
            HealthCheckUrl = "http://localhost:5060/health",
            SystemdServiceName = "text-to-speech.service",
            ProjectPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Olbrasoft/TextToSpeech/src/TextToSpeech.Service/TextToSpeech.Service.csproj"
            )
        });
    }

    public async Task StartServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting dependent services...");

        foreach (var service in _services.Values)
        {
            try
            {
                var isHealthy = await CheckHealthAsync(service, cancellationToken);

                if (isHealthy)
                {
                    _logger.LogInformation("{ServiceName} is already running", service.Name);
                    service.IsRunning = true;
                    OnServiceStatusChanged(service.Name, true);
                    continue;
                }

                // Try to start via systemd first
                var started = await TryStartViaSystemdAsync(service, cancellationToken);

                if (!started)
                {
                    // Fallback to dotnet run
                    _logger.LogWarning("Systemd service {SystemdServiceName} not found, falling back to dotnet run",
                        service.SystemdServiceName);
                    await StartViaDotnetRunAsync(service, cancellationToken);
                }

                // Wait a bit for service to start
                await Task.Delay(2000, cancellationToken);

                // Verify service is healthy
                isHealthy = await CheckHealthAsync(service, cancellationToken);
                service.IsRunning = isHealthy;

                if (isHealthy)
                {
                    _logger.LogInformation("{ServiceName} started successfully", service.Name);
                }
                else
                {
                    _logger.LogError("{ServiceName} failed to start or become healthy", service.Name);
                }

                OnServiceStatusChanged(service.Name, isHealthy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start {ServiceName}", service.Name);
                service.IsRunning = false;
                OnServiceStatusChanged(service.Name, false);
            }
        }

        // Start monitoring
        _monitoringTask = MonitorServicesAsync(_monitoringCts.Token);
    }

    public async Task StopServicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping dependent services...");

        // Stop monitoring first
        _monitoringCts.Cancel();
        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        foreach (var service in _services.Values)
        {
            try
            {
                if (service.Process != null && !service.Process.HasExited)
                {
                    _logger.LogInformation("Stopping {ServiceName} (dotnet run process)", service.Name);
                    service.Process.Kill(entireProcessTree: true);
                    await service.Process.WaitForExitAsync(cancellationToken);
                    service.Process.Dispose();
                    service.Process = null;
                }
                else if (!string.IsNullOrEmpty(service.SystemdServiceName))
                {
                    // If started via systemd, we don't stop it (user may want to keep it running)
                    _logger.LogInformation("Skipping shutdown of systemd service {SystemdServiceName}",
                        service.SystemdServiceName);
                }

                service.IsRunning = false;
                OnServiceStatusChanged(service.Name, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop {ServiceName}", service.Name);
            }
        }
    }

    public IDictionary<string, bool> GetServicesStatus()
    {
        return _services.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.IsRunning);
    }

    public async Task RefreshServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Refreshing status for {ServiceName}", serviceName);

        var wasRunning = service.IsRunning;
        var isHealthy = await CheckHealthAsync(service, cancellationToken);
        service.IsRunning = isHealthy;

        if (wasRunning != isHealthy)
        {
            _logger.LogInformation("{ServiceName} status changed: {Status}",
                serviceName, isHealthy ? "Running" : "Stopped");
            OnServiceStatusChanged(serviceName, isHealthy);
        }
        else
        {
            _logger.LogInformation("{ServiceName} status confirmed: {Status}",
                serviceName, isHealthy ? "Running" : "Stopped");
            // Fire event even if status unchanged to update UI
            OnServiceStatusChanged(serviceName, isHealthy);
        }
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Starting {ServiceName}", serviceName);

        // Check if already running
        var isHealthy = await CheckHealthAsync(service, cancellationToken);
        if (isHealthy)
        {
            _logger.LogInformation("{ServiceName} is already running", serviceName);
            service.IsRunning = true;
            OnServiceStatusChanged(serviceName, true);
            return;
        }

        // Try to start via systemd first
        var started = await TryStartViaSystemdAsync(service, cancellationToken);

        if (!started)
        {
            // Fallback to dotnet run
            _logger.LogWarning("Systemd service {SystemdServiceName} not found, falling back to dotnet run",
                service.SystemdServiceName);
            await StartViaDotnetRunAsync(service, cancellationToken);
        }

        // Wait a bit for service to start
        await Task.Delay(2000, cancellationToken);

        // Verify service is healthy
        isHealthy = await CheckHealthAsync(service, cancellationToken);
        service.IsRunning = isHealthy;

        if (isHealthy)
        {
            _logger.LogInformation("{ServiceName} started successfully", serviceName);
        }
        else
        {
            _logger.LogError("{ServiceName} failed to start or become healthy", serviceName);
        }

        OnServiceStatusChanged(serviceName, isHealthy);
    }

    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogWarning("Service {ServiceName} not found", serviceName);
            return;
        }

        _logger.LogInformation("Stopping {ServiceName}", serviceName);

        bool stopped = false;

        // Try 1: Kill tracked process
        if (service.Process != null && !service.Process.HasExited)
        {
            _logger.LogInformation("Stopping {ServiceName} (dotnet run process)", serviceName);
            service.Process.Kill(entireProcessTree: true);
            await service.Process.WaitForExitAsync(cancellationToken);
            service.Process.Dispose();
            service.Process = null;
            stopped = true;
        }
        // Try 2: Stop via systemd
        else if (!string.IsNullOrEmpty(service.SystemdServiceName))
        {
            try
            {
                var stopProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "systemctl",
                        Arguments = $"--user stop {service.SystemdServiceName}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                stopProcess.Start();
                await stopProcess.WaitForExitAsync(cancellationToken);

                if (stopProcess.ExitCode == 0)
                {
                    _logger.LogInformation("Stopped {ServiceName} via systemd", serviceName);
                    stopped = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop {ServiceName} via systemd", serviceName);
            }
        }

        // Try 3: Find and kill process by port from HealthCheckUrl
        if (!stopped)
        {
            try
            {
                // Extract port from health check URL (e.g., "http://localhost:5060/health" -> 5060)
                var uri = new Uri(service.HealthCheckUrl);
                var port = uri.Port;

                _logger.LogInformation("Attempting to stop {ServiceName} by finding process on port {Port}", serviceName, port);

                // Use ss to find PID in LISTEN state on port (more reliable than lsof)
                // ss output: "users:(("TextToSpeech.Se",pid=607996,fd=247))"
                var ssProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ss",
                        Arguments = $"-tulpn sport = :{port}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                ssProcess.Start();
                var ssOutput = await ssProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                await ssProcess.WaitForExitAsync(cancellationToken);

                // Parse ss output to find PID in LISTEN state
                // Example line: "tcp   LISTEN 0  512  127.0.0.1:5060  0.0.0.0:*  users:(("TextToSpeech.Se",pid=607996,fd=247))"
                int? pid = null;
                foreach (var line in ssOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Contains("LISTEN") && line.Contains($":{port}"))
                    {
                        // Extract PID from users:((process,pid=XXXXX,fd=...))
                        var match = System.Text.RegularExpressions.Regex.Match(line, @"pid=(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedPid))
                        {
                            pid = parsedPid;
                            break; // Take first LISTEN socket (IPv4 or IPv6, doesn't matter)
                        }
                    }
                }

                if (pid.HasValue)
                {
                    _logger.LogInformation("Found {ServiceName} process with PID {Pid}, killing it", serviceName, pid.Value);

                    var killProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "kill",
                            Arguments = $"-TERM {pid.Value}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    killProcess.Start();
                    await killProcess.WaitForExitAsync(cancellationToken);

                    if (killProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully killed {ServiceName} (PID: {Pid})", serviceName, pid.Value);
                        stopped = true;
                    }
                }
                else
                {
                    _logger.LogWarning("No process found listening on port {Port} for {ServiceName}", port, serviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop {ServiceName} by port", serviceName);
            }
        }

        service.IsRunning = false;
        OnServiceStatusChanged(serviceName, false);
    }

    private async Task<bool> CheckHealthAsync(DependentServiceInfo service, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            var response = await httpClient.GetAsync(service.HealthCheckUrl, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> TryStartViaSystemdAsync(DependentServiceInfo service, CancellationToken cancellationToken)
    {
        try
        {
            // Check if systemd service exists
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"--user status {service.SystemdServiceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            // Exit code 4 = service not found, 3 = stopped, 0 = running
            if (checkProcess.ExitCode == 4)
            {
                return false; // Service doesn't exist
            }

            // Try to start it
            var startProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"--user start {service.SystemdServiceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            startProcess.Start();
            await startProcess.WaitForExitAsync(cancellationToken);

            if (startProcess.ExitCode == 0)
            {
                _logger.LogInformation("Started {ServiceName} via systemd", service.Name);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start {ServiceName} via systemd", service.Name);
            return false;
        }
    }

    private Task StartViaDotnetRunAsync(DependentServiceInfo service, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {service.ProjectPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(service.ProjectPath)
            };

            // Copy current environment variables to child process (required when UseShellExecute=false)
            foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
            {
                var key = envVar.Key?.ToString();
                var value = envVar.Value?.ToString();
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    startInfo.Environment[key] = value;
                }
            }

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogDebug("[{ServiceName}] {Output}", service.Name, e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogWarning("[{ServiceName}] {Error}", service.Name, e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            service.Process = process;

            _logger.LogInformation("Started {ServiceName} via dotnet run (PID: {ProcessId})",
                service.Name, process.Id);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ServiceName} via dotnet run", service.Name);
            throw;
        }
    }

    private async Task MonitorServicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting service health monitoring");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                foreach (var service in _services.Values)
                {
                    var wasRunning = service.IsRunning;
                    var isHealthy = await CheckHealthAsync(service, cancellationToken);
                    service.IsRunning = isHealthy;

                    if (wasRunning != isHealthy)
                    {
                        _logger.LogWarning("{ServiceName} status changed: {Status}",
                            service.Name, isHealthy ? "Running" : "Stopped");
                        OnServiceStatusChanged(service.Name, isHealthy);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during service health monitoring");
            }
        }

        _logger.LogInformation("Service health monitoring stopped");
    }

    private void OnServiceStatusChanged(string serviceName, bool isRunning)
    {
        ServiceStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs
        {
            ServiceName = serviceName,
            IsRunning = isRunning
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _monitoringCts.Cancel();
        _monitoringCts.Dispose();

        foreach (var service in _services.Values)
        {
            service.Process?.Dispose();
        }
    }

    private class DependentServiceInfo
    {
        public string Name { get; init; } = string.Empty;
        public string HealthCheckUrl { get; init; } = string.Empty;
        public string SystemdServiceName { get; init; } = string.Empty;
        public string ProjectPath { get; init; } = string.Empty;
        public bool IsRunning { get; set; }
        public Process? Process { get; set; }
    }
}
