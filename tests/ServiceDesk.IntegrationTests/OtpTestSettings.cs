using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Modules.Identity.Infrastructure.Options;

namespace ServiceDesk.IntegrationTests;

internal static class OtpTestSettings
{
    internal static string Key => Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
    internal static void AddTestOtpKey(this IServiceCollection services) =>
        services.Configure<OtpProtectionOptions>(options => options.Key = Key);
}
