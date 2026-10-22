using System.Net;
using System.Text;
using System.Text.Json;
using Dejavu.Core.Models;

namespace Dejavu.Core.Services;

public class ApiServerService
{
    private readonly LoggerService _logger;
    private readonly IUwfController _uwf;
    private readonly ThawOnceService _thawOnce;
    private readonly MaintenanceService _maintenance;
    private HttpListener? _listener;
    private bool _running;

    public ApiServerService(LoggerService logger, IUwfController uwf, ThawOnceService thawOnce, MaintenanceService maintenance)
    {
        _logger = logger;
        _uwf = uwf;
        _thawOnce = thawOnce;
        _maintenance = maintenance;
    }

    public void Start(int port, string authToken, CancellationToken ct = default)
    {
        if (!HttpListener.IsSupported)
        {
            _logger.Log(LogLevel.Error, "[API] HttpListener não é suportado neste ambiente.");
            throw new PlatformNotSupportedException("HttpListener não suportado.");
        }

        _listener = new HttpListener();
        string prefix = $"http://+:{port}/";
        _listener.Prefixes.Add(prefix);

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            _logger.Log(LogLevel.Warning, $"[API] Falha ao registrar prefixo curinga http://+:{port}/. Tentando http://localhost:{port}/. Erro: {ex.Message}");
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
        }

        _running = true;
        _logger.Log(LogLevel.Info, $"[API] Servidor Dejavu iniciado na porta {port}. Aguardando requisições...");

        while (_running && !ct.IsCancellationRequested)
        {
            try
            {
                var context = _listener.GetContext();
                ProcessRequest(context, authToken);
            }
            catch (Exception ex) when (!_running || ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "[API] Erro ao atender requisição HTTP.", ex);
            }
        }
    }

    public void Stop()
    {
        _running = false;
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _logger.Log(LogLevel.Info, "[API] Servidor Dejavu finalizado.");
    }

    private void ProcessRequest(HttpListenerContext context, string expectedToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Validação de Token Bearer
            if (!string.IsNullOrEmpty(expectedToken))
            {
                string authHeader = request.Headers["Authorization"] ?? "";
                string bearerPrefix = "Bearer ";
                string providedToken = authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring(bearerPrefix.Length).Trim()
                    : request.QueryString["token"] ?? "";

                if (providedToken != expectedToken)
                {
                    SendJson(response, HttpStatusCode.Unauthorized, new { error = "Não autorizado. Token inválido ou ausente." });
                    return;
                }
            }

            string path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
            string method = request.HttpMethod.ToUpperInvariant();

            switch (path)
            {
                case "/api/v1/status":
                    if (method != "GET")
                    {
                        SendJson(response, HttpStatusCode.MethodNotAllowed, new { error = "Método não permitido." });
                        return;
                    }
                    var status = _uwf.GetStatus();
                    SendJson(response, HttpStatusCode.OK, status);
                    break;

                case "/api/v1/freeze":
                    if (method != "POST")
                    {
                        SendJson(response, HttpStatusCode.MethodNotAllowed, new { error = "Método não permitido." });
                        return;
                    }
                    string volume = request.QueryString["volume"] ?? "C:";
                    bool restartFreeze = bool.TryParse(request.QueryString["restart"], out bool rf) && rf;
                    _uwf.Freeze(volume);
                    _logger.Log(LogLevel.Info, $"[API] Comando Freeze acionado remotamente para volume {volume}.");
                    if (restartFreeze) _uwf.RestartSystem();
                    SendJson(response, HttpStatusCode.OK, new { status = "OK", action = "freeze", volume, restart = restartFreeze });
                    break;

                case "/api/v1/thaw":
                    if (method != "POST")
                    {
                        SendJson(response, HttpStatusCode.MethodNotAllowed, new { error = "Método não permitido." });
                        return;
                    }
                    bool once = bool.TryParse(request.QueryString["once"], out bool ro) && ro;
                    bool restartThaw = bool.TryParse(request.QueryString["restart"], out bool rt) && rt;
                    _uwf.Thaw();
                    if (once)
                    {
                        _thawOnce.ArmThawOnce();
                        _logger.Log(LogLevel.Info, "[API] Comando Thaw-Once acionado remotamente.");
                    }
                    else
                    {
                        _logger.Log(LogLevel.Info, "[API] Comando Thaw permanente acionado remotamente.");
                    }
                    if (restartThaw) _uwf.RestartSystem();
                    SendJson(response, HttpStatusCode.OK, new { status = "OK", action = once ? "thaw-once" : "thaw", restart = restartThaw });
                    break;

                case "/api/v1/restart":
                    if (method != "POST")
                    {
                        SendJson(response, HttpStatusCode.MethodNotAllowed, new { error = "Método não permitido." });
                        return;
                    }
                    _logger.Log(LogLevel.Info, "[API] Reinicialização remota solicitada.");
                    _uwf.RestartSystem();
                    SendJson(response, HttpStatusCode.OK, new { status = "OK", action = "restart" });
                    break;

                case "/api/v1/maintenance":
                    if (method != "POST")
                    {
                        SendJson(response, HttpStatusCode.MethodNotAllowed, new { error = "Método não permitido." });
                        return;
                    }
                    _logger.Log(LogLevel.Info, "[API] Disparo de manutenção agendada remota.");
                    _maintenance.ScheduleMaintenanceWindow();
                    SendJson(response, HttpStatusCode.OK, new { status = "OK", action = "maintenance_scheduled" });
                    break;

                default:
                    SendJson(response, HttpStatusCode.NotFound, new { error = "Endpoint não encontrado." });
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "[API] Exceção interna no tratamento de requisição.", ex);
            SendJson(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
        }
    }

    private static void SendJson(HttpListenerResponse response, HttpStatusCode statusCode, object data)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
    }
}
