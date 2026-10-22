using System.Net.Http.Headers;
using System.Text.Json;
using Dejavu.Core.Models;

namespace Dejavu.Core.Services;

public class ClusterNodeResult
{
    public string Host { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public UwfStatus? Status { get; set; }
}

public class ClusterManagerService
{
    private readonly LoggerService _logger;
    private readonly HttpClient _httpClient;

    public ClusterManagerService(LoggerService logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<List<ClusterNodeResult>> ExecuteActionAsync(List<string> hosts, int port, string authToken, string endpoint, HttpMethod method)
    {
        var tasks = hosts.Select(host => RunNodeActionAsync(host.Trim(), port, authToken, endpoint, method));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private async Task<ClusterNodeResult> RunNodeActionAsync(string host, int port, string authToken, string endpoint, HttpMethod method)
    {
        string url = $"http://{host}:{port}{endpoint}";
        try
        {
            using var req = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(authToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            using var response = await _httpClient.SendAsync(req);
            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new ClusterNodeResult
                {
                    Host = host,
                    Success = false,
                    Message = $"HTTP {(int)response.StatusCode}: {body}"
                };
            }

            if (endpoint.Contains("/status"))
            {
                var status = JsonSerializer.Deserialize<UwfStatus>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new ClusterNodeResult
                {
                    Host = host,
                    Success = true,
                    Status = status,
                    Message = "OK"
                };
            }

            return new ClusterNodeResult
            {
                Host = host,
                Success = true,
                Message = body
            };
        }
        catch (Exception ex)
        {
            return new ClusterNodeResult
            {
                Host = host,
                Success = false,
                Message = ex.Message
            };
        }
    }

    public static string GenerateHtmlReport(List<ClusterNodeResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Dejavu IFPR — Painel do Laboratório</title>");
        sb.AppendLine("<style>body{font-family:sans-serif;margin:20px;background:#f8f9fa;color:#333} table{width:100%;border-collapse:collapse;margin-top:20px;background:#fff} th,td{border:1px solid #ddd;padding:10px;text-align:left} th{background:#005c28;color:#fff} .badge-frozen{background:#17a2b8;color:#fff;padding:4px 8px;border-radius:4px} .badge-thawed{background:#ffc107;color:#000;padding:4px 8px;border-radius:4px} .badge-err{background:#dc3545;color:#fff;padding:4px 8px;border-radius:4px}</style></head><body>");
        sb.AppendLine($"<h1>Dejavu IFPR — Painel de Estado das Máquinas</h1><p>Atualizado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("<table><thead><tr><th>Máquina</th><th>Estado Atual</th><th>Próximo Boot</th><th>Thaw-Once</th><th>Overlay Usado</th><th>Status / Detalhes</th></tr></thead><tbody>");

        foreach (var r in results)
        {
            if (r.Success && r.Status != null)
            {
                string curClass = r.Status.CurrentEnabled ? "badge-frozen" : "badge-thawed";
                string curText = r.Status.CurrentEnabled ? "FROZEN" : "THAWED";
                string nxtText = r.Status.NextEnabled ? "FROZEN" : "THAWED";
                string thawOnce = r.Status.ThawOncePending ? "SIM" : "Não";
                string overlayUsage = $"{r.Status.Overlay.CurrentUsageMb} / {r.Status.Overlay.MaximumSizeMb} MB";

                sb.AppendLine($"<tr><td><strong>{r.Host}</strong></td><td><span class='{curClass}'>{curText}</span></td><td>{nxtText}</td><td>{thawOnce}</td><td>{overlayUsage}</td><td>OK</td></tr>");
            }
            else
            {
                sb.AppendLine($"<tr><td><strong>{r.Host}</strong></td><td colspan='4'><span class='badge-err'>FALHA</span></td><td>{r.Message}</td></tr>");
            }
        }

        sb.AppendLine("</tbody></table></body></html>");
        return sb.ToString();
    }

    public static string GenerateCsvReport(List<ClusterNodeResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Host,Success,CurrentEnabled,NextEnabled,ThawOncePending,OverlayUsedMb,OverlayMaxMb,Details");
        foreach (var r in results)
        {
            if (r.Success && r.Status != null)
            {
                sb.AppendLine($"{r.Host},{r.Success},{r.Status.CurrentEnabled},{r.Status.NextEnabled},{r.Status.ThawOncePending},{r.Status.Overlay.CurrentUsageMb},{r.Status.Overlay.MaximumSizeMb},\"OK\"");
            }
            else
            {
                sb.AppendLine($"{r.Host},false,,,,,,\"{r.Message?.Replace("\", "'")}\"");
            }
        }
        return sb.ToString();
    }
}
