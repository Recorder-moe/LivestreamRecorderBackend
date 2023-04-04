using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace LivestreamRecorderBackend.Helper;

internal static class Database
{
    internal static (DbContext, IUnitOfWork) MakeDBContext<T>() where T : DbContext, new()
    {
        string databaseName = typeof(T).Name.Replace("Context", string.Empty);

        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_" + databaseName);
        if (string.IsNullOrEmpty(connectionString)) throw new InvalidOperationException("Database connectionString is not set!!");

        var contextOptions = new DbContextOptionsBuilder<T>()
                                     .UseCosmos(connectionString: connectionString,
                                                databaseName: databaseName)
                                     .Options;
        DbContext context = Activator.CreateInstance(typeof(T), new object[] { contextOptions }) as DbContext
            ?? throw new InvalidOperationException("Failed to create DBContext!!");
        var unitOfWork = new UnitOfWork(context);
        return (context, unitOfWork);
    }
}
