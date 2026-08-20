namespace OpenMock;

public class MockDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public int StatusCode { get; set; } = 200;
    public string ContentType { get; set; } = "application/json";
    public string Body { get; set; } = "";
}

public class RequestHit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MockId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = "";

    public static async Task<RequestHit> FromRequest(HttpRequest request, Guid? mockId)
    {
        request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(request.Body, leaveOpen: true))
        {
            var buffer = new char[64 * 1024];
            var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            body = new string(buffer, 0, read);
        }
        request.Body.Position = 0;

        return new RequestHit
        {
            MockId = mockId,
            Method = request.Method,
            Path = request.Path + request.QueryString,
            Headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body = body
        };
    }
}
