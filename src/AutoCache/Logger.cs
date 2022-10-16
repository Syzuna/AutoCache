using Microsoft.Extensions.Logging;
using System;
using System.Threading;

public static class Logger
{
    public static void LogD(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"Thread:{Thread.CurrentThread.ManagedThreadId} Time:{DateTime.Now.TimeOfDay} {message}");
    }

    public static void LogI(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(message);
    }

    public static void LogE(this ILogger logger, string message)
    {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError($"Thread:{Thread.CurrentThread.ManagedThreadId} Time:{DateTime.Now.TimeOfDay} {message}");
    }

    
}