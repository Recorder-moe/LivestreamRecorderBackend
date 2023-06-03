﻿using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;
using System.Security.Claims;

namespace LivestreamRecorderBackend.Helper;

public static class Log
{
    private static ILogger? _logger;

    public static ILogger Logger
    {
        get
        {
            if (null == _logger
                || _logger.GetType() != typeof(Serilog.Core.Logger))
            {
                _logger = MakeLogger();
            }
            return _logger;
        }
        set => _logger = value;
    }

    public static ILogger MakeLogger()
    {
        Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));

        var logger = new LoggerConfiguration()
                        .MinimumLevel.Verbose()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
                        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Fatal)
                        .MinimumLevel.Override("System", LogEventLevel.Fatal)
                        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}",
                                         restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .WriteTo.Seq(serverUrl: Environment.GetEnvironmentVariable("Seq_ServerUrl")!,
                                     apiKey: Environment.GetEnvironmentVariable("Seq_ApiKey"),
                                     restrictedToMinimumLevel: LogEventLevel.Verbose)
                        .Enrich.FromLogContext()
                        .CreateLogger();
        return logger;
    }

    public static void LogClaimsPrincipal(ClaimsPrincipal principal)
        => Logger.Debug(JsonConvert.SerializeObject(principal.Claims.Select(p => (p.Type, p.Value)).ToArray()));

}
