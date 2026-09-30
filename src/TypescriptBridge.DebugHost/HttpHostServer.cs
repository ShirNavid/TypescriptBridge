using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TypescriptBridge.DebugHost
{
    // A minimal HTTP server bound to loopback that serves the files
    // inside a single session directory. This is the replacement for
    // the Node.js server that the Tool generates when --mode debug is
    // used. The VSIX uses this server instead of the Tool's Node
    // server so that no Node.js installation is required on the
    // developer's machine.
    //
    // Responsibilities:
    //   - bind to 127.0.0.1 on a fixed port
    //   - serve files inside the session root only
    //   - reject path traversal
    //   - return correct Content-Type for html/js/map/json
    public sealed class HttpHostServer : IDisposable
    {
        private readonly string _root;
        private readonly int _port;
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;

        public HttpHostServer(string sessionRoot, int port)
        {
            _root = Path.GetFullPath(sessionRoot);
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _cts = new CancellationTokenSource();
        }

        // Starts the listener and the accept loop.
        public void Start()
        {
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        // Stops the listener.
        public void Stop()
        {
            try { _cts.Cancel(); } catch { /* ignore */ }
            try { _listener.Stop(); } catch { /* ignore */ }
        }

        public void Dispose()
        {
            Stop();
            try { _listener.Close(); } catch { /* ignore */ }
            _cts.Dispose();
        }

        // Accept loop. Runs until cancellation is requested.
        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                // Handle each request on a separate task so that a slow
                // client does not block the accept loop.
                _ = Task.Run(() => HandleRequest(context));
            }
        }

        // Serves a single HTTP request.
        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var urlPath = context.Request.Url?.AbsolutePath ?? "/";
                if (urlPath == "/" || urlPath.Length == 0)
                {
                    urlPath = "/index.html";
                }

                // Decode URL-encoded characters before building the file path.
                urlPath = Uri.UnescapeDataString(urlPath);

                // Strip the leading slash and resolve inside the root.
                var relative = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var requested = Path.GetFullPath(Path.Combine(_root, relative));

                // Path traversal guard: the resolved path must be inside the root.
                if (!requested.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(requested, _root, StringComparison.OrdinalIgnoreCase))
                {
                    WriteText(context, 403, "text/plain", "Forbidden");
                    return;
                }

                if (!File.Exists(requested))
                {
                    WriteText(context, 404, "text/plain", "Not found");
                    return;
                }

                var bytes = File.ReadAllBytes(requested);
                var mime = GetMimeType(Path.GetExtension(requested));

                context.Response.StatusCode = 200;
                context.Response.ContentType = mime;
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch
            {
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { /* ignore */ }
            }
        }

        // Writes a small text response.
        private static void WriteText(HttpListenerContext context, int status, string mime, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            context.Response.StatusCode = status;
            context.Response.ContentType = mime;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        // Returns a content type for the given file extension.
        private static string GetMimeType(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js":   return "application/javascript; charset=utf-8";
                case ".map":  return "application/json; charset=utf-8";
                case ".ts":   return "application/typescript; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".css":  return "text/css; charset=utf-8";
                default:      return "application/octet-stream";
            }
        }
    }
}

