using OpenMock;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var store = new MockStore(Path.Combine(app.Environment.ContentRootPath, "mocks.json"));
var feed = new LiveFeed();

app.UseWebSockets();

// Live request feed for the UI.
app.Map("/ws", (HttpContext ctx) => feed.HandleAsync(ctx));

// Admin API for managing mocks.
var api = app.MapGroup("/api/mocks");
api.MapGet("/", () => store.All());
api.MapPost("/", (MockDefinition mock) => Results.Ok(store.Add(mock)));
api.MapPut("/{id:guid}", (Guid id, MockDefinition mock) =>
    store.Update(id, mock) is { } updated ? Results.Ok(updated) : Results.NotFound());
api.MapDelete("/{id:guid}", (Guid id) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound());

app.UseDefaultFiles();
app.UseStaticFiles();
// UseDefaultFiles skips "/" because MapFallback claims it — serve the UI explicitly.
app.MapGet("/", () => Results.Redirect("/index.html"));

// Everything else goes through the mock matcher.
app.MapFallback(async ctx =>
{
    var mock = store.Match(ctx.Request.Method, ctx.Request.Path.Value ?? "/");
    var hit = await RequestHit.FromRequest(ctx.Request, mock?.Id);
    await feed.Broadcast(hit);

    if (mock is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsync($"No mock matched {ctx.Request.Method} {ctx.Request.Path}");
        return;
    }

    ctx.Response.StatusCode = mock.StatusCode;
    ctx.Response.ContentType = mock.ContentType;
    await ctx.Response.WriteAsync(mock.Body);
});

app.Run();
