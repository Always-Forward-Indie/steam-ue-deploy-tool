using System.Collections.Concurrent;
using System.Text.Json;
using SteamUEDeployTool.Core.Abstractions;
using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Infrastructure.Storage;

public sealed class ProfileRepository : IProfileRepository
{
    private readonly string _basePath;
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _locks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileRepository(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamUEDeployTool",
            "profiles");

        Directory.CreateDirectory(_basePath);
    }

    public IReadOnlyList<T> GetAll<T>() where T : class
    {
        return GetAllAsync<T>().GetAwaiter().GetResult();
    }

    public T? GetById<T>(Guid id) where T : class
    {
        return GetByIdAsync<T>(id).GetAwaiter().GetResult();
    }

    public void Save<T>(T entity) where T : class
    {
        SaveAsync(entity).GetAwaiter().GetResult();
    }

    public bool Delete<T>(Guid id) where T : class
    {
        return DeleteAsync<T>(id).GetAwaiter().GetResult();
    }

    public async ValueTask<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken ct = default) where T : class
    {
        var filePath = GetFilePath<T>();
        if (!File.Exists(filePath))
            return Array.Empty<T>();

        var json = await File.ReadAllTextAsync(filePath, ct);
        var items = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
        return items ?? [];
    }

    public async ValueTask<T?> GetByIdAsync<T>(Guid id, CancellationToken ct = default) where T : class
    {
        var all = await GetAllAsync<T>(ct);
        return all.FirstOrDefault(item =>
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop?.GetValue(item) is Guid itemId)
                return itemId == id;
            return false;
        });
    }

    public async ValueTask SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var lockObj = _locks.GetOrAdd(typeof(T), _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(ct);

        try
        {
            var all = (await GetAllAsync<T>(ct)).ToList();
            var prop = typeof(T).GetProperty("Id");
            var entityId = prop?.GetValue(entity) as Guid?;
            var modifiedProp = typeof(T).GetProperty("ModifiedAt");

            if (entityId is not null)
            {
                var existingIndex = all.FindIndex(item =>
                {
                    var itemId = prop?.GetValue(item) as Guid?;
                    return itemId == entityId;
                });

                if (existingIndex >= 0)
                {
                    modifiedProp?.SetValue(entity, DateTime.UtcNow);
                    all[existingIndex] = entity;
                }
                else
                {
                    var createdAtProp = typeof(T).GetProperty("CreatedAt");
                    createdAtProp?.SetValue(entity, DateTime.UtcNow);
                    modifiedProp?.SetValue(entity, DateTime.UtcNow);
                    all.Add(entity);
                }
            }
            else
            {
                all.Add(entity);
            }

            var filePath = GetFilePath<T>();
            var json = JsonSerializer.Serialize(all, JsonOptions);

            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync<T>(Guid id, CancellationToken ct = default) where T : class
    {
        var lockObj = _locks.GetOrAdd(typeof(T), _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(ct);

        try
        {
            var all = (await GetAllAsync<T>(ct)).ToList();
            var prop = typeof(T).GetProperty("Id");
            var removed = all.RemoveAll(item =>
            {
                var itemId = prop?.GetValue(item) as Guid?;
                return itemId == id;
            });

            if (removed == 0)
                return false;

            var filePath = GetFilePath<T>();
            var json = JsonSerializer.Serialize(all, JsonOptions);

            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, filePath, overwrite: true);

            return true;
        }
        finally
        {
            lockObj.Release();
        }
    }

    private string GetFilePath<T>() where T : class
    {
        var typeName = typeof(T).Name.ToLowerInvariant();
        return Path.Combine(_basePath, $"{typeName}s.json");
    }
}
