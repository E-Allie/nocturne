using System.Net;

namespace Nocturne.Connectors.Twiist.Tests.Services;

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    public RecordingHttpMessageHandler Enqueue(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public RecordingHttpMessageHandler EnqueueJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json)
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_responses.Count == 0)
            throw new InvalidOperationException("No fake HTTP response was queued.");

        return _responses.Dequeue()(request);
    }
}
