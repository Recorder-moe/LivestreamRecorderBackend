using LivestreamRecorder.DB.Core;
using LivestreamRecorder.DB.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace LivestreamRecorderBackend.Helper;

internal static class Database
{
    internal static (DbContext, UnitOfWork) MakeDBContext<TDbContext, TUnitOfWork>()
        where TDbContext : DbContext, new()
        where TUnitOfWork : UnitOfWork
    {
        string databaseName = typeof(TDbContext).Name.Replace("Context", string.Empty);

        string? connectionString = Environment.GetEnvironmentVariable("ConnectionStrings_" + databaseName);
        if (string.IsNullOrEmpty(connectionString)) throw new InvalidOperationException("Database connectionString is not set!!");

        var contextOptions = new DbContextOptionsBuilder<TDbContext>()
                                     .UseCosmos(connectionString: connectionString,
                                                databaseName: databaseName)
                                     .Options;
        DbContext context = Activator.CreateInstance(typeof(TDbContext), new object[] { contextOptions }) as DbContext
            ?? throw new InvalidOperationException("Failed to create DBContext!!");
        UnitOfWork unitOfWork = Activator.CreateInstance(typeof(TUnitOfWork), new object[] { context }) as UnitOfWork
            ?? throw new InvalidOperationException("Failed to create UnitOfWork!!");

        return (context, unitOfWork);
    }
}
