using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace PrintFleet.WorkTracker.Api.Services;

public class GraphMailOptions
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string CertificateThumbprint { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public bool UseManagedIdentity { get; set; }
    public string ManagedIdentityClientId { get; set; } = "";
}

public sealed record GraphSendResult(bool Accepted, bool Transient, int StatusCode, string? ErrorType);

public sealed class GraphMailException : Exception
{
    public GraphMailException(GraphSendResult result)
        : base(result.ErrorType ?? "Graph mail was not accepted.")
    {
        Transient = result.Transient;
        ErrorType = result.ErrorType ?? "GraphRejected";
        StatusCode = result.StatusCode;
    }

    public bool Transient { get; }
    public string ErrorType { get; }
    public int StatusCode { get; }
}

public interface IGraphMailbox
{
    bool IsConfigured { get; }
    string Readiness { get; }
    string? Sender { get; }
    Task<GraphSendResult> ProbeAsync(CancellationToken ct);
    Task<GraphSendResult> SendAsync(string to, string subject, string? text, string? html, string? replyTo, string? replyName, CancellationToken ct);
}

public class GraphMailbox : IGraphMailbox
{
    private const string Scope = "https://graph.microsoft.com/.default";
    private readonly GraphMailOptions _graph;
    private readonly EmailOptions _email;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<GraphMailbox> _logger;
    private readonly Lazy<TokenCredential?> _credential;

    public GraphMailbox(IOptions<GraphMailOptions> graph, IOptions<EmailOptions> email, IHttpClientFactory http, ILogger<GraphMailbox> logger)
    {
        _graph = graph.Value;
        _email = email.Value;
        _http = http;
        _logger = logger;
        _credential = new Lazy<TokenCredential?>(CreateCredential);
    }

    public string? Sender => string.IsNullOrWhiteSpace(_email.FromAddress) ? null : _email.FromAddress.Trim();

    public bool IsConfigured => Readiness == "Ready";

    public string Readiness
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_graph.TenantId) && !_graph.UseManagedIdentity) return "MissingTenant";
            if (string.IsNullOrWhiteSpace(_graph.ClientId) && !_graph.UseManagedIdentity) return "MissingClient";
            if (string.IsNullOrWhiteSpace(Sender)) return "MissingSender";
            if (_graph.UseManagedIdentity) return "Ready";
            if (string.IsNullOrWhiteSpace(_graph.CertificateThumbprint) && string.IsNullOrWhiteSpace(_graph.CertificatePath))
                return "MissingCertificate";
            return _credential.Value is null ? "CertificateUnavailable" : "Ready";
        }
    }

    public async Task<GraphSendResult> ProbeAsync(CancellationToken ct)
    {
        if (!IsConfigured || _credential.Value is null)
            return new GraphSendResult(false, false, 0, Readiness);
        try
        {
            await _credential.Value.GetTokenAsync(new TokenRequestContext([Scope]), ct);
            return new GraphSendResult(true, false, 200, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Outlook token probe failed with {ErrorType}", ex.GetType().Name);
            return new GraphSendResult(false, false, 0, ex.GetType().Name);
        }
    }

    public async Task<GraphSendResult> SendAsync(string to, string subject, string? text, string? html, string? replyTo, string? replyName, CancellationToken ct)
    {
        if (!IsConfigured || _credential.Value is null || Sender is null)
            return new GraphSendResult(false, false, 0, Readiness);

        var token = await _credential.Value.GetTokenAsync(new TokenRequestContext([Scope]), ct);
        var htmlBody = !string.IsNullOrWhiteSpace(html);
        var message = new Dictionary<string, object>
        {
            ["subject"] = subject,
            ["body"] = new Dictionary<string, string>
            {
                ["contentType"] = htmlBody ? "HTML" : "Text",
                ["content"] = htmlBody ? html! : text ?? ""
            },
            ["toRecipients"] = new[]
            {
                new Dictionary<string, object> { ["emailAddress"] = new Dictionary<string, string> { ["address"] = to } }
            }
        };
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message["replyTo"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["emailAddress"] = new Dictionary<string, string> { ["address"] = replyTo, ["name"] = replyName ?? replyTo }
                }
            };
        }
        var payload = new Dictionary<string, object>
        {
            ["message"] = message,
            ["saveToSentItems"] = true
        };

        var client = _http.CreateClient("graph");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(Sender)}/sendMail");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Accepted)
            return new GraphSendResult(true, false, 202, null);

        var code = await ReadErrorCode(response, ct);
        var status = (int)response.StatusCode;
        var transient = status is 408 or 429 or >= 500;
        return new GraphSendResult(false, transient, status, code ?? $"Http{status}");
    }

    private TokenCredential? CreateCredential()
    {
        try
        {
            if (_graph.UseManagedIdentity)
            {
                return string.IsNullOrWhiteSpace(_graph.ManagedIdentityClientId)
                    ? new ManagedIdentityCredential()
                    : new ManagedIdentityCredential(_graph.ManagedIdentityClientId.Trim());
            }

            var certificate = LoadCertificate();
            if (certificate is null) return null;
            return new ClientCertificateCredential(_graph.TenantId.Trim(), _graph.ClientId.Trim(), certificate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Outlook certificate could not be loaded. {ErrorType}", ex.GetType().Name);
            return null;
        }
    }

    private X509Certificate2? LoadCertificate()
    {
        if (!string.IsNullOrWhiteSpace(_graph.CertificatePath))
        {
            var path = _graph.CertificatePath.Trim();
            return string.IsNullOrEmpty(_graph.CertificatePassword)
                ? X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.EphemeralKeySet)
                : X509CertificateLoader.LoadPkcs12FromFile(path, _graph.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        }

        var thumbprint = _graph.CertificateThumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (thumbprint.Length == 0) return null;
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            if (matches.Count > 0) return new X509Certificate2(matches[0]);
        }
        return null;
    }

    private static async Task<string?> ReadErrorCode(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code))
                return code.GetString();
        }
        catch
        {
            return null;
        }
        return null;
    }
}
