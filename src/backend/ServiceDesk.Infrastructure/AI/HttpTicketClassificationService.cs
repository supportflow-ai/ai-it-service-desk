using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.AIAssistance.Contracts;
using ServiceDesk.Application.AIAssistance.Ports;
using ServiceDesk.Infrastructure.AI.Contracts.V1;
using ServiceDesk.Infrastructure.Options;

namespace ServiceDesk.Infrastructure.AI;

internal sealed class HttpTicketClassificationService(
    HttpClient client, IOptions<AiOptions> configured, ClassificationCircuit circuit) : ITicketClassificationService
{
    private const int MaxResponseBytes = 65_536;

    public async Task<TriageResult> ClassifyAsync(TriageRequest snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = ClassificationContractMapper.ToRequest(snapshot, Guid.NewGuid());
        if (circuit.IsOpen) return TriageResult.Unavailable("ai_circuit_open");
        var options = configured.Value;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            // Phân loại không ghi dữ liệu: chỉ retry một lần với cùng requestId, cùng ngân sách.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post,
                    new Uri(new Uri(options.Endpoint), "/v1/ticket-classifications"));
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
                message.Content = JsonContent.Create(request);
                using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, budget.Token);
                if (attempt == 0 && response.StatusCode is HttpStatusCode.BadGateway or
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), budget.Token);
                    continue;
                }
                if (!response.IsSuccessStatusCode) return Failed(TriageResult.Unavailable("ai_http_unavailable"));
                if (response.Content.Headers.ContentLength > MaxResponseBytes ||
                    response.Content.Headers.ContentType?.MediaType != "application/json")
                    return Failed(TriageResult.InvalidResponse("ai_response_invalid"));

                await using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
                using var bytes = new MemoryStream();
                var buffer = new byte[4096];
                int read;
                while ((read = await stream.ReadAsync(buffer, budget.Token)) > 0)
                {
                    if (bytes.Length + read > MaxResponseBytes)
                        return Failed(TriageResult.InvalidResponse("ai_response_too_large"));
                    bytes.Write(buffer, 0, read);
                }
                var result = ClassificationContractMapper.FromJson(request, Encoding.UTF8.GetString(bytes.ToArray()));
                circuit.Record(result.Outcome == TriageOutcome.Success);
                return result;
            }
            return Failed(TriageResult.Unavailable("ai_http_unavailable"));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Failed(TriageResult.Timeout("ai_timeout")); }
        catch (HttpRequestException)
        { return Failed(TriageResult.Unavailable("ai_transport_unavailable")); }
        catch (IOException)
        { return Failed(TriageResult.Unavailable("ai_transport_unavailable")); }
    }

    private TriageResult Failed(TriageResult result) { circuit.Record(false); return result; }
}
