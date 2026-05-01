 using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Greenhouse_Control.Services
{
    public sealed class GreenhouseClient : IAsyncDisposable
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _listenCts;
        private int _disconnectStarted;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public event Action<string>? LineReceived;
        public event Action? Disconnected;

        public bool IsConnected => _client is { Connected: true };

        public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                return;
            }

            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _disconnectStarted = 0;

            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            _listenCts = new CancellationTokenSource();
            _ = Task.Run(() => ListenAsync(_listenCts.Token));
        }

        public async Task SendLineAsync(string line, CancellationToken cancellationToken = default)
        {
            if (_writer is null)
            {
                return;
            }

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await DisconnectCoreAsync().ConfigureAwait(false);
        }

        private async Task DisconnectCoreAsync()
        {
            if (Interlocked.Exchange(ref _disconnectStarted, 1) == 1)
            {
                return;
            }

            _listenCts?.Cancel();
            _listenCts = null;

            if (_writer is not null)
            {
                try
                {
                    await _writer.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The remote endpoint may already have closed the socket.
                }
            }

            _reader?.Dispose();
            _writer?.Dispose();
            _client?.Close();

            _reader = null;
            _writer = null;
            _client = null;

            Disconnected?.Invoke();
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _sendLock.Dispose();
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _reader is not null)
                {
                    var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }

                    LineReceived?.Invoke(line);
                }
            }
            catch
            {
                // Ignore socket shutdown exceptions.
            }
            finally
            {
                await DisconnectCoreAsync().ConfigureAwait(false);
            }
        }
    }
}

