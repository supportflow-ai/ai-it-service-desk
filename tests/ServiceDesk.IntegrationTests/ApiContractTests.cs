using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ServiceDesk.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class ApiContractTests
{
    [Fact]
    public async Task PublishedApiMatchesBaseline()
    {
        await using var factory = new ContractFactory();
        using var client = factory.CreateClient();
        var actual = JsonNode.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"))!;
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../../contracts/backend/openapi-baseline.json");
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
                foreach (var service in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                             d.ImplementationType?.Name == "IdentityDataSeeder").ToArray())
                    services.Remove(service);
            });
        }
    }
}
