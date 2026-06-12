namespace SteamUEDeployTool.Core.Abstractions;

public interface IProfileRepository
{
    IReadOnlyList<T> GetAll<T>() where T : class;
    T? GetById<T>(Guid id) where T : class;
    void Save<T>(T entity) where T : class;
    bool Delete<T>(Guid id) where T : class;
    ValueTask SaveAsync<T>(T entity, CancellationToken ct = default) where T : class;
    ValueTask<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken ct = default) where T : class;
    ValueTask<T?> GetByIdAsync<T>(Guid id, CancellationToken ct = default) where T : class;
    ValueTask<bool> DeleteAsync<T>(Guid id, CancellationToken ct = default) where T : class;
}
