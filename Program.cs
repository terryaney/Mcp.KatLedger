using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using KatLedger;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            KatLedgerStore store = KatLedgerStore.OpenCanonical();

            if (args.Any(static arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                SelfTestRunner.Run(store);
                Console.Error.WriteLine("KatLedger self-test passed.");
                return 0;
            }

            JsonSerializerOptions serializerOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly(typeof(Program).Assembly, serializerOptions);

            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
