# SentinelGrid

**ASP.NET Core 8** minimal API + native WebSockets. Simulated perimeter nodes stream ok/warn/crit events into a live console page.

## Run
```bash
dotnet run
# http://localhost:5000
```

## Shape
- `GET /` dashboard
- `WS  /hub` event fabric
- `GET /health` probe

Swap the `Task.Run` generator for your SIEM or MQTT ingest.
