namespace PrintFleet.WorkTracker.Api.Services;

public class RoutingEmailSender : IEmailSender
{
    private readonly SmtpEmailSender _smtp;
    private readonly IGraphMailbox _graph;

    public RoutingEmailSender(SmtpEmailSender smtp, IGraphMailbox graph)
    {
        _smtp = smtp;
        _graph = graph;
    }

    public bool CanSend => _graph.IsConfigured || _smtp.CanSend;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (_graph.IsConfigured)
            await SendGraph(to, subject, body, null, null, null, ct);
        else
            await _smtp.SendAsync(to, subject, body, ct);
    }

    public async Task SendRichAsync(string to, string subject, string text, string html, string? replyTo, string? replyName, CancellationToken ct)
    {
        if (_graph.IsConfigured)
            await SendGraph(to, subject, text, html, replyTo, replyName, ct);
        else
            await _smtp.SendRichAsync(to, subject, text, html, replyTo, replyName, ct);
    }

    private async Task SendGraph(string to, string subject, string? text, string? html, string? replyTo, string? replyName, CancellationToken ct)
    {
        var result = await _graph.SendAsync(to, subject, text, html, replyTo, replyName, ct);
        if (!result.Accepted) throw new GraphMailException(result);
    }
}
