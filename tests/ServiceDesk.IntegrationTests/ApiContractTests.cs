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
    public async Task ExistingApiMatchesPublishedBaseline()
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
