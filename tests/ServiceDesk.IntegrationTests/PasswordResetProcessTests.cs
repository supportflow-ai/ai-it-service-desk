using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Application.Contracts;
using ServiceDesk.Modules.Identity.Application.Ports;
using ServiceDesk.Platform.Persistence;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PasswordResetProcessTests(TicketIdempotencyPostgresFixture fixture) : IClassFixture<TicketIdempotencyPostgresFixture>
{
    [Fact]
    public async Task KilledApiAfterSmtpAcceptanceResumesTheSameOtpAndRefreshSession()
    {
        await using var smtp = new SmtpSink();
        string? refreshToken;
        Guid jobId;
        Guid otpId;
        var email = $"restart-{Guid.NewGuid():N}@test.invalid";
        await using (var first = await ApiProcess.StartAsync(fixture.ConnectionString, smtp.Port))
        {
            using var register = await first.Client.PostAsJsonAsync("/api/auth/register", new { email, password = "Test@1234" });
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
            using var login = await first.Client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test@1234" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            refreshToken = (await login.Content.ReadFromJsonAsync<AuthResult>())!.RefreshToken;
            using var request = await first.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.OK, request.StatusCode);
            await smtp.Accepted.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));
            await using var context = fixture.CreateContext();
            var job = await context.PasswordResetJobs.SingleAsync(j => j.Email == email);
            Assert.Null(job.CompletedAt);
            Assert.NotNull(job.ProtectedCode);
            jobId = job.Id;
            otpId = job.OtpCodeId!.Value;
            // Dispose giết đúng child API trước khi SMTP trả ACK; không chạy shutdown graceful.
        }
        smtp.ReleaseFirstAck();
        await using (var context = fixture.CreateContext())
            await context.PasswordResetJobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseUntil, DateTime.UtcNow.AddSeconds(-1)));
        await using (var second = await ApiProcess.StartAsync(fixture.ConnectionString, smtp.Port))
        {
            var secondCode = await smtp.Accepted.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20));
            await UntilAsync(async () =>
            {
                await using var context = fixture.CreateContext();
                return await context.PasswordResetJobs.AnyAsync(j => j.Id == jobId && j.CompletedAt != null);
            });
            using var refresh = await second.Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            using var reset = await second.Client.PostAsJsonAsync("/api/auth/reset-password", new { email, otpCode = secondCode, newPassword = "Changed@1234" });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }
        Assert.Equal(2, smtp.Codes.Count);
        Assert.Single(smtp.Codes.Distinct());
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.OtpCodes.CountAsync(o => o.Target == email));
        Assert.True((await verify.OtpCodes.SingleAsync(o => o.Id == otpId)).IsUsed);
        Assert.Null((await verify.PasswordResetJobs.SingleAsync(j => j.Id == jobId)).ProtectedCode);
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        Assert.Fail("Tiến trình API không đạt điều kiện chờ trong 20 giây.");
    }

    private sealed class ApiProcess : IAsyncDisposable
    {
        private readonly Process process;
        public HttpClient Client { get; }
        private ApiProcess(Process process, int port)
        {
            this.process = process;
            Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(10) };
        }

        public static async Task<ApiProcess> StartAsync(string connectionString, int smtpPort)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(typeof(ServiceDesk.Api.Program).Assembly.Location);
            foreach (var pair in new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production", ["DOTNET_ENVIRONMENT"] = "Production",
                ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}", ["Database__ConnectionString"] = connectionString,
                ["Jwt__SecretKey"] = TicketIdempotencyPostgresFixture.Secret, ["Jwt__Issuer"] = "ServiceDesk", ["Jwt__Audience"] = "ServiceDeskClient",
                ["Otp__Key"] = OtpTestSettings.Key, ["Email__Smtp__Host"] = "127.0.0.1", ["Email__Smtp__Port"] = smtpPort.ToString(),
                ["Email__Smtp__EnableSsl"] = "false", ["Email__Smtp__FromAddress"] = "sender@test.invalid"
            }) start.Environment[pair.Key] = pair.Value;
            var process = Process.Start(start)!;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var api = new ApiProcess(process, port);
            try
            {
                await UntilAsync(async () =>
                {
                    if (process.HasExited) throw new InvalidOperationException($"Child API thoát với mã {process.ExitCode}.");
                    try { using var response = await api.Client.GetAsync("/health/live"); return response.IsSuccessStatusCode; }
                    catch (HttpRequestException) { return false; }
                });
                return api;
            }
            catch { await api.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token);
            }
            process.Dispose();
            Client.Dispose();
        }
    }

    private sealed class SmtpSink : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly TaskCompletionSource firstAck = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task server;
        public Channel<string> Accepted { get; } = Channel.CreateUnbounded<string>();
        public List<string> Codes { get; } = [];
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public SmtpSink() { listener.Start(); server = ServeAsync(); }
        public void ReleaseFirstAck() => firstAck.TrySetResult();

        private async Task ServeAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var peer = await listener.AcceptTcpClientAsync(stop.Token);
                    using var reader = new StreamReader(peer.GetStream(), Encoding.ASCII);
                    await using var writer = new StreamWriter(peer.GetStream(), Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
                    await writer.WriteLineAsync("220 localhost");
                    while (await reader.ReadLineAsync(stop.Token) is { } command)
                    {
                        if (command == "DATA")
                        {
                            await writer.WriteLineAsync("354 data");
                            var body = new StringBuilder();
                            while (await reader.ReadLineAsync(stop.Token) is { } line && line != ".") body.AppendLine(line);
                            var message = body.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
                            var end = message.IndexOf("\n\n", StringComparison.Ordinal);
                            var content = message[(end + 2)..];
                            if (message[..end].Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
                                content = Encoding.UTF8.GetString(Convert.FromBase64String(content));
                            var code = Regex.Match(content, @"(?<![0-9])[0-9]{6}(?![0-9])").Value;
                            Assert.Equal(6, code.Length);
                            Codes.Add(code);
                            await Accepted.Writer.WriteAsync(code, stop.Token);
                            if (Codes.Count == 1) await firstAck.Task.WaitAsync(stop.Token);
                            await writer.WriteLineAsync("250 accepted");
                        }
                        else if (command == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
                        else await writer.WriteLineAsync("250 localhost");
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
                catch (IOException) { /* Child bị kill sau DATA; tiếp tục nhận từ process mới. */ }
            }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            try { await server.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (SocketException) when (stop.IsCancellationRequested) { }
            stop.Dispose();
        }
    }
}
