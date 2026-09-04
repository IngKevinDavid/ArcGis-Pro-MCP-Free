using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibreMcpAddin
{
    // TCP transport for the MCP bridge: listens on 127.0.0.1:PORT only
    // (loopback, never exposed to the LAN). Same 4-byte little-endian
    // length-prefixed JSON framing as the old named pipe, so the protocol
    // is unchanged - only the transport moved to TCP.
    public class PipeServer
    {
        public const int DefaultPort = 5876;
        public const string LoopbackHost = "127.0.0.1";
        private const int MaxRequestBytes = 10 * 1024 * 1024;
        private const int MaxListeners = 4;

        private readonly int _port;
        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private List<Task> _listeners;

        public int Port => _port;
        public string Endpoint => LoopbackHost + ":" + _port;
        public bool IsRunning => _listeners != null;

        public PipeServer() : this(DefaultPort) { }

        public PipeServer(int port)
        {
            _port = (port >= 1 && port <= 65535) ? port : DefaultPort;
        }

        public PipeServer(string portText) : this(ParsePort(portText)) { }

        public static int ParsePort(string text)
        {
            int p;
            return (int.TryParse((text ?? "").Trim(), out p) && p >= 1 && p <= 65535)
                ? p : DefaultPort;
        }

        public void Start()
        {
            if (IsRunning) return;

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listeners = new List<Task>();
            for (int i = 0; i < MaxListeners; i++)
                _listeners.Add(Task.Run(() => ListenerLoopAsync(_cts.Token)));
            System.Diagnostics.Debug.WriteLine("Libre MCP TCP bridge listening on " + Endpoint);
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                if (_cts != null) _cts.Cancel();
                if (_listener != null) _listener.Stop();
                if (_listeners != null) Task.WaitAll(_listeners.ToArray(), 1500);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error stopping TCP bridge: " + ex.Message);
            }
            finally
            {
                if (_cts != null) _cts.Dispose();
                _cts = null;
                _listener = null;
                _listeners = null;
                System.Diagnostics.Debug.WriteLine("Libre MCP TCP bridge stopped.");
            }
        }

        private async Task ListenerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();

                    if (token.IsCancellationRequested)
                    {
                        client.Close();
                        break;
                    }

                    // Serve this client without blocking the listener.
                    _ = HandleClientAsync(client, token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (client != null) client.Close();
                    if (token.IsCancellationRequested) break;
                    try { await Task.Delay(200, token); } catch (OperationCanceledException) { break; }
                }
                catch (Exception ex)
                {
                    if (client != null) client.Close();
                    System.Diagnostics.Debug.WriteLine("TCP bridge accept error: " + ex.Message);
                    try { await Task.Delay(200, token); } catch (OperationCanceledException) { break; }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] lengthBytes = await ReadExactlyAsync(stream, sizeof(int), token);
                    int requestLength = BitConverter.ToInt32(lengthBytes, 0);

                    if (requestLength <= 0 || requestLength > MaxRequestBytes)
                    {
                        string errorMessage = requestLength <= 0
                            ? "Invalid request length received from client."
                            : "Request of " + requestLength + " bytes exceeds the maximum allowed size of " + MaxRequestBytes + " bytes.";
                        await WriteResponseAsync(stream, CommandHandler.SerializeError(errorMessage, "INVALID_REQUEST"), token);
                        return;
                    }

                    byte[] requestBytes = await ReadExactlyAsync(stream, requestLength, token);
                    string requestJson = Encoding.UTF8.GetString(requestBytes);
                    string responseJson = await CommandHandler.HandleAsync(requestJson);

                    await WriteResponseAsync(stream, responseJson, token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TCP bridge client error: " + ex.Message);
            }
        }

        private static async Task WriteResponseAsync(NetworkStream stream, string responseJson, CancellationToken token)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
            byte[] responseLength = BitConverter.GetBytes(responseBytes.Length);

            await stream.WriteAsync(responseLength, 0, responseLength.Length, token);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
            await stream.FlushAsync(token);
        }

        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int length, CancellationToken token)
        {
            byte[] buffer = new byte[length];
            int offset = 0;

            while (offset < length)
            {
                int read = await stream.ReadAsync(buffer, offset, length - offset, token);
                if (read == 0)
                {
                    throw new EndOfStreamException("The client disconnected before the full request was read.");
                }

                offset += read;
            }

            return buffer;
        }
    }
}
