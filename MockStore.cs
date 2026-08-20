using System.Text.Json;

namespace OpenMock;

// In-memory mock storage with JSON-file persistence.
public class MockStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private List<MockDefinition> _mocks = new();

    public MockStore(string file)
    {
        _file = file;
        if (File.Exists(file))
        {
            try
            {
                _mocks = JsonSerializer.Deserialize<List<MockDefinition>>(File.ReadAllText(file)) ?? new();
            }
            catch
            {
                _mocks = new();
            }
        }
    }

    public IReadOnlyList<MockDefinition> All()
    {
        lock (_lock) return _mocks.ToList();
    }

    public MockDefinition Add(MockDefinition mock)
    {
        mock.Id = Guid.NewGuid();
        Normalize(mock);
        lock (_lock)
        {
            _mocks.Add(mock);
            Save();
        }
        return mock;
    }

    public MockDefinition? Update(Guid id, MockDefinition mock)
    {
        lock (_lock)
        {
            var existing = _mocks.FirstOrDefault(m => m.Id == id);
            if (existing is null) return null;
            existing.Method = mock.Method;
            existing.Path = mock.Path;
            existing.StatusCode = mock.StatusCode;
            existing.ContentType = mock.ContentType;
            existing.Body = mock.Body;
            Normalize(existing);
            Save();
            return existing;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var removed = _mocks.RemoveAll(m => m.Id == id) > 0;
            if (removed) Save();
            return removed;
        }
    }

    public MockDefinition? Match(string method, string path)
    {
        lock (_lock)
        {
            return _mocks.FirstOrDefault(m =>
                string.Equals(m.Method, method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void Normalize(MockDefinition mock)
    {
        mock.Method = mock.Method.ToUpperInvariant();
        if (!mock.Path.StartsWith('/')) mock.Path = "/" + mock.Path;
    }

    private void Save()
    {
        File.WriteAllText(_file, JsonSerializer.Serialize(_mocks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
