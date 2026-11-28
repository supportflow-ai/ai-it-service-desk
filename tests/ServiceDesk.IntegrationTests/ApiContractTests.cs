using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class ApiContractTests
{
    [Theory]
    [InlineData("/api/auth/register", "post", "201,400,429")]
    [InlineData("/api/auth/login", "post", "200,401,429")]
    [InlineData("/api/auth/refresh", "post", "200,401,429")]
    [InlineData("/api/auth/revoke", "post", "204,429")]
    [InlineData("/api/auth/reset-password", "post", "200,400,429")]
    [InlineData("/api/auth/change-password", "post", "200,400,401")]
    [InlineData("/api/auth/me", "get", "200,401,404")]
    public async Task IdentityDocumentsItsActualResponseCodes(string path, string method, string codes)
    {
        await using var factory = new ContractFactory();
        using var client = factory.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var responses = document["paths"]![path]![method]!["responses"]!.AsObject();
        foreach (var code in codes.Split(',')) Assert.True(responses.ContainsKey(code), $"{path} {code}");
        if (path is "/api/auth/register" or "/api/auth/revoke") Assert.False(responses.ContainsKey("200"));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(true, 604801)]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(false, 604801)]
    public void InvalidAttachmentTtlFailsAtStartup(bool upload, int seconds)
    {
        using var factory = new ContractFactory();
        using var invalid = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<ServiceDesk.Platform.Options.R2Options>(options =>
            {
                if (upload) options.UploadExpirySeconds = seconds;
                else options.DownloadExpirySeconds = seconds;
            })));
        Assert.ThrowsAny<Microsoft.Extensions.Options.OptionsValidationException>(() => invalid.CreateClient());
    }

    [Fact]
    public async Task NullableSlaReferenceIsDocumentedAsNullable()
    {
        await using var factory = new ContractFactory();
        using var client = factory.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var sla = document["components"]!["schemas"]!["TicketDto"]!["properties"]!["sla"]!;
        Assert.True(sla["nullable"]?.GetValue<bool>() == true);
        Assert.Equal("#/components/schemas/SlaSummaryDto", sla["allOf"]![0]!["$ref"]!.GetValue<string>());
    }

    [Fact]
    public async Task TriageReadDocumentsBothRequesterAndStaffShapes()
    {
        await using var factory = new ContractFactory();
        using var client = factory.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var schema = document["paths"]!["/api/tickets/{id}/triage"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
        // Requester là tập con trường của Agent: oneOf sẽ loại phản hồi hợp lệ khớp cả hai schema.
        Assert.NotNull(schema["anyOf"]);
        var variants = schema["anyOf"]!.AsArray().Select(s => s!["$ref"]!.GetValue<string>()).Order().ToArray();
        Assert.Equal(new[] { "#/components/schemas/AgentTriageView", "#/components/schemas/RequesterTriageView" }, variants);
    }

    [Fact]
    public async Task PublishedApiMatchesBaseline()
    {
        await using var factory = new ContractFactory();
        using var client = factory.CreateClient();
        var actual = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var assemblyDirectory = Path.GetDirectoryName(typeof(ApiContractTests).Assembly.Location)!;
        var path = Path.Combine(assemblyDirectory, "../../../../../contracts/backend/openapi-baseline.json");
        if (Environment.GetEnvironmentVariable("EXPORT_API_BASELINE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual.ToJsonString(new() { WriteIndented = true }));
        }
        var expected = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        foreach (var endpoint in expected["paths"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(endpoint.Value, actual["paths"]![endpoint.Key]), endpoint.Key);
        foreach (var schema in expected["components"]!["schemas"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(schema.Value, actual["components"]!["schemas"]![schema.Key]), schema.Key);

        // So sánh toàn bộ tài liệu để phát hiện cả API/schema mới bị bỏ sót
        // và thay đổi mô tả, cấu hình bảo mật hoặc các phần OpenAPI khác.
        Assert.True(JsonNode.DeepEquals(expected, actual),
            "Tài liệu OpenAPI thực tế khác bản chuẩn đã công bố. Hãy xem xét toàn bộ thay đổi hợp đồng trước khi cập nhật bản chuẩn.");
    }

    private sealed class ContractFactory : WebApplicationFactory<ServiceDesk.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.AddTestOtpKey();
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                             d.ImplementationType?.Name == "PasswordResetQueueProcessor").ToArray())
                    services.Remove(descriptor);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                foreach (var service in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                             d.ImplementationType?.Name == "IdentityDataSeeder").ToArray())
                    services.Remove(service);
            });
        }
    }
}
