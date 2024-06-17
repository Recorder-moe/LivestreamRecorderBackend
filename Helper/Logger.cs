using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace LivestreamRecorderBackend.Helper;

public static class Log
{
    private static ILogger? _logger;

    public static ILogger Logger
    {
        get
        {
            if (null == _logger
                || _logger.GetType() != typeof(Logger))
                _logger = MakeLogger();

            return _logger;
        }
        set => _logger = value;
    }

    public static ILogger MakeLogger()
    {
        SelfLog.Enable(Console.WriteLine);

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);

        Logger logger = new LoggerConfiguration()
                        .MinimumLevel.ControlledBy(levelSwitch)
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Fatal)
                        .MinimumLevel.Override("System", LogEventLevel.Fatal)
                        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}",
                                         theme: AnsiConsoleTheme.Code)
                        .WriteTo.Seq(serverUrl: Environment.GetEnvironmentVariable("Seq_ServerUrl")!,
                                     apiKey: Environment.GetEnvironmentVariable("Seq_ApiKey"),
                                     controlLevelSwitch: levelSwitch)
                        .Enrich.WithMachineName()
                        .Enrich.FromLogContext()
                        .CreateLogger();

        return logger;
    }

    public static void LogClaimsPrincipal(ClaimsPrincipal principal)
    {
        Logger.Debug(JsonSerializer.Serialize(principal.Claims.Select(p => (p.Type, p.Value)).ToArray()));
    }
}
