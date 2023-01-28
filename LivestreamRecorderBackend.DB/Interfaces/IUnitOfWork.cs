using LivestreamRecorderBackend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorderBackend.DB.Interfaces;

public interface IUnitOfWork
{
    DbContext Context { get; set; }

    void Commit();

    T ReloadEntityFromDB<T>(T entity) where T : Entity;
}
