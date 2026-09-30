using System.Net;
using System.Net.Sockets;

namespace TypescriptBridge.DebugHost
{
    // Finds a free TCP port bound to loopback so that the HTTP host
    // server and the CDP endpoint do not collide with other tools.
    public static class PortAllocator
    {
        // Returns a currently free port on 127.0.0.1.
        //
        // The port is chosen by binding to port 0, reading the assigned
        // port, then closing the listener. This is inherently racy but
        // acceptable for a developer-machine debug host.
        public static int FindFreeLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                return endpoint.Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        // Returns true if the given TCP port is free on loopback.
        // Used by the runtime host to fail with a clear error before
        // trying to bind the HTTP listener.
        public static bool IsFree(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                try { listener.Stop(); } catch { /* ignore */ }
            }
        }
    }
}


