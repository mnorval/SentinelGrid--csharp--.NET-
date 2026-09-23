using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseWebSockets();

var alerts = new ConcurrentQueue<object>();
var sockets = new ConcurrentBag<WebSocket>();

app.MapGet("/", () => Results.Content("""
<!doctype html><meta charset="utf-8"><title>SentinelGrid</title>
<style>
 body{margin:0;font-family:ui-sans-serif,system-ui;background:#070b14;color:#d7e3ff}
 header{padding:18px 24px;border-bottom:1px solid #1c2a4a;letter-spacing:.12em}
 #log{padding:16px;font:13px ui-monospace,monospace}
 .crit{color:#fb7185}.warn{color:#fbbf24}.ok{color:#34d399}
</style>
<header>SENTINELGRID · live perimeter</header>
<div id="log"></div>
<script>
 const log = document.getElementById('log');
 const ws = new WebSocket((location.protocol==='https:'?'wss':'ws')+'://'+location.host+'/hub');
 ws.onmessage = e => {
   const a = JSON.parse(e.data);
   const div = document.createElement('div');
   div.className = a.level;
   div.textContent = a.ts.slice(11,19)+'  ['+a.level+']  '+a.node+'  '+a.msg;
   log.prepend(div);
 };
</script>
""", "text/html"));

app.Map("/hub", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    sockets.Add(ws);
    var buf = new byte[8];
    while (ws.State == WebSocketState.Open)
    {
        var r = await ws.ReceiveAsync(buf, ctx.RequestAborted);
        if (r.MessageType == WebSocketMessageType.Close) break;
    }
});

_ = Task.Run(async () =>
{
    var rng = Random.Shared;
    string[] nodes = ["edge-cpt", "edge-jnb", "core-dbn", "gw-ams"];
    string[] msgs = ["tls probe dropped", "auth anomaly", "heartbeat ok", "rate-limit armed", "cert expiring"];
    string[] levels = ["ok", "warn", "crit"];
    while (true)
    {
        var ev = new {
            ts = DateTimeOffset.UtcNow,
            node = nodes[rng.Next(nodes.Length)],
            level = levels[rng.Next(levels.Length)],
            msg = msgs[rng.Next(msgs.Length)]
        };
        alerts.Enqueue(ev);
        var json = JsonSerializer.Serialize(ev);
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var ws in sockets)
        {
            if (ws.State != WebSocketState.Open) continue;
            try { await ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
            catch { /* peer gone */ }
        }
        await Task.Delay(700);
    }
});

app.MapGet("/health", () => Results.Ok(new { service = "SentinelGrid", status = "live" }));
app.Run();
