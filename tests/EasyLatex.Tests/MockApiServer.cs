using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

internal sealed class MockApiServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cancel = new();
    public string Endpoint { get; }
    public string Old { get; set; } = "Every good paper starts with one clear idea.";
    public string New { get; set; } = "Every clear paper starts with one good idea.";
    public int DelayMilliseconds { get; set; }
    public int StatusCode { get; set; } = 200;
    public int Requests { get; private set; }
    public string LastPayload { get; private set; } = "";
    public MockApiServer()
    {
        _listener.Start(); Endpoint = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1";
        _ = AcceptLoop();
    }
    private async Task AcceptLoop()
    {
        try { while (!_cancel.IsCancellationRequested) { var client = await _listener.AcceptTcpClientAsync(_cancel.Token); _ = Respond(client); } }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task Respond(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream(); var header = new List<byte>(); var one = new byte[1];
                while (header.Count < 32_000)
                {
                    if (await stream.ReadAsync(one, _cancel.Token) == 0) return;
                    header.Add(one[0]);
                    if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                var count = int.Parse(lines.First(l => l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                var body = new byte[count]; await stream.ReadExactlyAsync(body, _cancel.Token); LastPayload = Encoding.UTF8.GetString(body); Requests++;
                var delay = DelayMilliseconds; var status = StatusCode;
                var content = JsonSerializer.Serialize(new { explanation = "这是本机验证服务提供的修改建议。", changes = new[] { new { old = Old, @new = New } } });
                var result = status == 200 ? JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } }) : "{\"error\":{\"message\":\"test authentication failed\"}}";
                if (delay > 0) await Task.Delay(delay, _cancel.Token);
                var bytes = Encoding.UTF8.GetBytes(result);
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : "Unauthorized")}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, _cancel.Token); await stream.WriteAsync(bytes, _cancel.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
    public void Dispose() { _cancel.Cancel(); _listener.Stop(); _cancel.Dispose(); }
}
