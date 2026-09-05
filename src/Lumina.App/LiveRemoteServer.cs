using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Lumina.App;

/// <summary>Pad metadata exposed to the remote web UI.</summary>
public sealed record LiveRemotePadInfo(string Name, int Frames);

public sealed record LiveRemoteConfig(int PadCount, IReadOnlyList<LiveRemotePadInfo> Pads);

/// <summary>
/// Tiny HTTP control surface for the Live engine.
/// Uses TcpListener (no Windows URL ACL / admin) so iPad can reach the PC on LAN.
/// </summary>
public sealed class LiveRemoteServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource _cts = new();
    private readonly Func<LiveRemoteConfig> _getConfig;
    private readonly Func<int, string?> _getPreviewSvg;
    private static byte[]? _logoPng;

    private readonly Action<int, int> _onPadPress;
    private readonly Action<int> _onPadRelease;
    private readonly Action _onPanic;
    private readonly Action<int, int, int> _onNoteOn;
    private readonly Action<int, int, int> _onCc;

    public int Port { get; private set; }

    public LiveRemoteServer(
        Func<LiveRemoteConfig> getConfig,
        Func<int, string?> getPreviewSvg,
        Action<int, int> onPadPress,
        Action<int> onPadRelease,
        Action onPanic,
        Action<int, int, int> onNoteOn,
        Action<int, int, int> onCc)
    {
        _getConfig = getConfig;
        _getPreviewSvg = getPreviewSvg;
        _onPadPress = onPadPress;
        _onPadRelease = onPadRelease;
        _onPanic = onPanic;
        _onNoteOn = onNoteOn;
        _onCc = onCc;
    }

    /// <summary>IPv4 addresses usable from another device on the same network.</summary>
    public static IReadOnlyList<string> GetLanIpv4Addresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("127.", StringComparison.Ordinal)) continue;
                    if (!list.Contains(ip, StringComparer.Ordinal))
                        list.Add(ip);
                }
            }
        }
        catch
        {
            // Fallback below.
        }

        if (list.Count == 0)
        {
            try
            {
                foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (a.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = a.ToString();
                    if (ip.StartsWith("127.", StringComparison.Ordinal)) continue;
                    if (!list.Contains(ip, StringComparer.Ordinal))
                        list.Add(ip);
                }
            }
            catch { /* ignore */ }
        }

        return list;
    }

    public static string PreferredLanIp()
    {
        var ips = GetLanIpv4Addresses();
        var preferred = ips.FirstOrDefault(ip =>
            ip.StartsWith("192.168.", StringComparison.Ordinal) ||
            ip.StartsWith("10.", StringComparison.Ordinal) ||
            Is172Private(ip));
        return preferred ?? ips.FirstOrDefault() ?? "127.0.0.1";
    }

    private static bool Is172Private(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var second)) return false;
        return parts[0] == "172" && second is >= 16 and <= 31;
    }

    public void Start(int port)
    {
        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        Stop();
        _cts = new CancellationTokenSource();
        Port = port;
        _ = EnsureLogoBytes();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { /* ignore */ }
        _listener = null;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(50, CancellationToken.None);
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                stream.ReadTimeout = 8000;
                stream.WriteTimeout = 8000;

                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                var method = parts[0].ToUpperInvariant();
                var pathRaw = parts[1];
                var path = pathRaw.Split('?', 2)[0].TrimEnd('/');
                if (path.Length == 0) path = "/";

                var contentLength = 0;
                while (true)
                {
                    var header = await reader.ReadLineAsync();
                    if (header is null || header.Length == 0) break;
                    if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(header.AsSpan(15).Trim(), out var len))
                    {
                        contentLength = Math.Max(0, len);
                    }
                }

                string bodyText = "";
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    var read = 0;
                    while (read < contentLength)
                    {
                        var n = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read));
                        if (n <= 0) break;
                        read += n;
                    }
                    bodyText = new string(buffer, 0, read);
                }

                await DispatchAsync(stream, method, path, bodyText);
            }
            catch
            {
                // Client disconnect / malformed request.
            }
        }
    }

    private async Task DispatchAsync(NetworkStream stream, string method, string path, string bodyText)
    {
        try
        {
            if (path is "/" or "/index.html")
            {
                await WriteResponseAsync(stream, 200, "text/html; charset=utf-8", HtmlPage());
                return;
            }

            if (path is "/logo.png" or "/logo")
            {
                var logo = EnsureLogoBytes();
                if (logo is null || logo.Length == 0)
                {
                    await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "logo missing");
                    return;
                }

                await WriteBinaryAsync(stream, 200, "image/png", logo);
                return;
            }

            if (path.StartsWith("/preview/", StringComparison.OrdinalIgnoreCase))
            {
                var slotText = path["/preview/".Length..].Replace(".svg", "", StringComparison.OrdinalIgnoreCase);
                if (!int.TryParse(slotText, out var slot))
                {
                    await WriteResponseAsync(stream, 400, "text/plain; charset=utf-8", "bad slot");
                    return;
                }

                var svg = _getPreviewSvg(slot) ?? """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160"><rect width="100%" height="100%" rx="10" fill="#070B12"/></svg>""";
                await WriteResponseAsync(stream, 200, "image/svg+xml; charset=utf-8", svg);
                return;
            }

            if (path == "/config")
            {
                var cfg = _getConfig();
                var json = JsonSerializer.Serialize(new
                {
                    padCount = cfg.PadCount,
                    pads = cfg.Pads.Select(p => new { name = p.Name, frames = p.Frames }).ToArray()
                });
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", json);
                return;
            }

            if (path == "/panic")
            {
                _onPanic();
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", """{"ok":true}""");
                return;
            }

            if (path == "/pad/press" && method == "POST")
            {
                var body = ParseJson(bodyText);
                var slot = body.GetProperty("slot").GetInt32();
                var velocity = body.TryGetProperty("velocity", out var v) ? v.GetInt32() : 100;
                _onPadPress(slot, velocity);
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", """{"ok":true}""");
                return;
            }

            if (path == "/pad/release" && method == "POST")
            {
                var body = ParseJson(bodyText);
                var slot = body.GetProperty("slot").GetInt32();
                _onPadRelease(slot);
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", """{"ok":true}""");
                return;
            }

            if (path == "/note" && method == "POST")
            {
                var body = ParseJson(bodyText);
                var note = body.GetProperty("note").GetInt32();
                var velocity = body.GetProperty("velocity").GetInt32();
                var channel = body.TryGetProperty("channel", out var ch) ? ch.GetInt32() : 1;
                _onNoteOn(note, velocity, channel);
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", """{"ok":true}""");
                return;
            }

            if (path == "/cc" && method == "POST")
            {
                var body = ParseJson(bodyText);
                var number = body.GetProperty("cc").GetInt32();
                var value = body.GetProperty("value").GetInt32();
                var channel = body.TryGetProperty("channel", out var ch) ? ch.GetInt32() : 1;
                _onCc(number, value, channel);
                await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", """{"ok":true}""");
                return;
            }

            await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "Not found");
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(stream, 500, "text/plain; charset=utf-8", ex.Message);
        }
    }

    private static byte[]? EnsureLogoBytes()
    {
        if (_logoPng is not null) return _logoPng;
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/lumina-icon.png", UriKind.Absolute);
            var info = Application.GetResourceStream(uri);
            if (info?.Stream is null) return null;
            using var ms = new MemoryStream();
            info.Stream.CopyTo(ms);
            _logoPng = ms.ToArray();
            return _logoPng;
        }
        catch
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "favicon-32.png");
                if (File.Exists(path))
                {
                    _logoPng = File.ReadAllBytes(path);
                    return _logoPng;
                }
            }
            catch { /* ignore */ }
            return null;
        }
    }

    private static JsonElement ParseJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return JsonDocument.Parse("{}").RootElement;
        using var doc = JsonDocument.Parse(text);
        return JsonDocument.Parse(doc.RootElement.GetRawText()).RootElement;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string contentType, string body)
    {
        await WriteBinaryAsync(stream, status, contentType, Encoding.UTF8.GetBytes(body ?? ""));
    }

    private static async Task WriteBinaryAsync(NetworkStream stream, int status, string contentType, byte[] payload)
    {
        var reason = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };
        var header =
            $"HTTP/1.1 {status} {reason}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-cache\r\n" +
            "Connection: close\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private static string HtmlPage() =>
        """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"/>
          <meta name="apple-mobile-web-app-capable" content="yes"/>
          <title>Lumina Live</title>
          <link rel="icon" href="/logo.png"/>
          <style>
            :root { color-scheme: dark; }
            * { box-sizing: border-box; }
            body {
              font-family: system-ui, -apple-system, "SF Pro Text", Segoe UI, sans-serif;
              background: #070B12;
              color: #F5FBFF;
              margin: 0;
              min-height: 100dvh;
              display: flex;
              flex-direction: column;
            }
            header {
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 12px;
              padding: 10px 16px;
              background: linear-gradient(180deg, #121A26 0%, #0C121C 100%);
              border-bottom: 1px solid rgba(58,168,184,.25);
              position: sticky;
              top: 0;
              z-index: 2;
            }
            .brand {
              display: flex;
              align-items: center;
              gap: 10px;
              min-width: 0;
            }
            .brand img {
              width: 40px;
              height: 40px;
              border-radius: 10px;
              flex-shrink: 0;
              box-shadow: 0 0 12px rgba(0, 229, 255, .25);
            }
            .brand .title {
              font-size: 17px;
              font-weight: 650;
              letter-spacing: .02em;
              white-space: nowrap;
            }
            .small { opacity: .75; font-size: 12px; font-weight: 500; }
            main {
              flex: 1;
              display: flex;
              flex-direction: column;
              align-items: center;
              padding: 18px 14px 20px;
              gap: 16px;
            }
            .grid {
              display: flex;
              flex-wrap: wrap;
              justify-content: center;
              align-content: flex-start;
              gap: 12px;
              width: 100%;
              max-width: 920px;
            }
            button.pad {
              width: 148px;
              min-height: 168px;
              border-radius: 14px;
              background: #121A26;
              color: white;
              font-weight: 600;
              border: 1px solid rgba(58,168,184,.45);
              touch-action: manipulation;
              display: flex;
              flex-direction: column;
              align-items: center;
              justify-content: flex-start;
              padding: 10px 8px 8px;
              gap: 6px;
              -webkit-user-select: none;
              user-select: none;
            }
            button.pad.active {
              background: #163528;
              border-color: #3DDC97;
              box-shadow: 0 0 0 2px rgba(61,220,151,.35);
            }
            button.pad .preview {
              width: 120px;
              height: 120px;
              border-radius: 10px;
              background: #070B12;
              object-fit: contain;
              pointer-events: none;
            }
            button.pad .meta {
              width: 100%;
              text-align: center;
            }
            button.pad .num {
              font-size: 13px;
              font-weight: 700;
              opacity: .85;
            }
            button.pad .name {
              font-size: 12px;
              font-weight: 500;
              opacity: .95;
              line-height: 1.25;
              max-height: 2.5em;
              overflow: hidden;
              word-break: break-word;
            }
            .panic-wrap {
              display: flex;
              justify-content: center;
              width: 100%;
              padding-top: 4px;
            }
            button.panic {
              width: auto;
              min-width: 148px;
              max-width: 220px;
              height: 36px;
              padding: 0 18px;
              border-radius: 10px;
              background: #6E1A1A;
              color: #FFECEC;
              font-weight: 650;
              font-size: 13px;
              letter-spacing: .04em;
              border: 1px solid #A33;
              touch-action: manipulation;
            }
            button.panic:active { background: #8B1E1E; }
          </style>
        </head>
        <body>
          <header>
            <div class="brand">
              <img src="/logo.png" alt="Lumina" width="40" height="40"/>
              <div class="title">Lumina Live</div>
            </div>
            <div class="small" id="status">…</div>
          </header>
          <main>
            <div class="grid" id="grid"></div>
            <div class="panic-wrap">
              <button class="panic" id="panic" type="button">Coupure</button>
            </div>
          </main>
          <script>
            const statusEl = document.getElementById('status');
            const gridEl = document.getElementById('grid');
            const panicEl = document.getElementById('panic');
            const active = new Set();
            async function config(){
              const r = await fetch('/config');
              return r.json();
            }
            function padButton(slot, label){
              const btn = document.createElement('button');
              btn.className = 'pad';
              btn.type = 'button';
              const img = document.createElement('img');
              img.className = 'preview';
              img.alt = label || ('Pad ' + (slot+1));
              img.src = '/preview/' + slot + '.svg?t=' + Date.now();
              const meta = document.createElement('div');
              meta.className = 'meta';
              const num = document.createElement('div');
              num.className = 'num';
              num.textContent = 'Pad ' + (slot+1);
              const name = document.createElement('div');
              name.className = 'name';
              name.textContent = label || '—';
              meta.appendChild(num);
              meta.appendChild(name);
              btn.appendChild(img);
              btn.appendChild(meta);
              btn.onpointerdown = async (ev) => {
                ev.preventDefault();
                if (active.has(slot)) return;
                active.add(slot);
                btn.classList.add('active');
                statusEl.textContent = 'pad ' + (slot+1);
                await fetch('/pad/press', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({slot, velocity: 120})});
              };
              const release = async () => {
                if (!active.has(slot)) return;
                active.delete(slot);
                btn.classList.remove('active');
                await fetch('/pad/release', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({slot})});
              };
              btn.onpointerup = release;
              btn.onpointercancel = release;
              btn.onpointerleave = release;
              return btn;
            }
            panicEl.onpointerdown = async (ev) => {
              ev.preventDefault();
              active.clear();
              gridEl.querySelectorAll('.pad.active').forEach(b => b.classList.remove('active'));
              statusEl.textContent = 'coupure';
              await fetch('/panic');
            };
            (async function main(){
              try{
                const c = await config();
                const n = c.padCount || 0;
                const pads = c.pads || [];
                gridEl.innerHTML = '';
                if (n === 0) {
                  statusEl.textContent = 'banque vide';
                  return;
                }
                for(let i=0;i<n;i++){
                  const label = pads[i] && pads[i].name ? pads[i].name : '';
                  if (label === '(vide)' && (!pads[i] || pads[i].frames === 0) && n === 1) {
                    statusEl.textContent = 'banque vide — chargez des ILDA dans Lumina';
                    return;
                  }
                  gridEl.appendChild(padButton(i, label));
                }
                statusEl.textContent = n + ' pad' + (n>1?'s':'');
              }catch(e){
                statusEl.textContent = 'erreur: '+e;
              }
            })();
          </script>
        </body>
        </html>
        """;

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
