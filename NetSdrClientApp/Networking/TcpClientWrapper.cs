using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public class TcpClientWrapper : ITcpClient, IDisposable
    {
        private readonly string _host;
        private readonly int _port;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            ThrowIfDisposed();

            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            // прибираємо попередню CTS (якщо була)
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _tcpClient = new TcpClient();

            try
            {
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");

                // запускаємо слухача з токеном цієї CTS
                _ = StartListeningAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                // при фейлі не тримаємо зайві ресурси
                _stream?.Dispose();
                _tcpClient?.Dispose();
                _cts?.Dispose();

                _stream = null;
                _tcpClient = null;
                _cts = null;
            }
        }

        public void Disconnect()
				{
					// не даємо викликати метод після Dispose()
					ThrowIfDisposed();

					if (!Connected)
					{
						Console.WriteLine("No active connection to disconnect.");
						return;
					}

					try
					{
						_cts?.Cancel();
						_stream?.Close();
						_tcpClient?.Close();
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error while disconnecting: {ex.Message}");
					}
					finally
					{
						_stream?.Dispose();
						_tcpClient?.Dispose();
						_cts?.Dispose();

						_stream = null;
						_tcpClient = null;
						_cts = null;
					}
				}
 
        public Task SendMessageAsync(byte[] data)
        {
            return SendMessageInternalAsync(data);
        }

        public Task SendMessageAsync(string str)
        {
            var data = Encoding.UTF8.GetBytes(str);
            return SendMessageInternalAsync(data);
        }

        private async Task SendMessageInternalAsync(byte[] data)
        {
            ThrowIfDisposed();

            if (Connected && _stream != null && _stream.CanWrite)
            {
                Console.WriteLine("Message sent: " +
                                  data.Select(b => Convert.ToString(b, toBase: 16))
                                      .Aggregate((l, r) => $"{l} {r}"));

                await _stream.WriteAsync(data, 0, data.Length);
            }
            else
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        private async Task StartListeningAsync(CancellationToken token)
        {
            if (_stream == null || !_stream.CanRead)
            {
                throw new InvalidOperationException("Not connected to a server.");
            }

            try
            {
                Console.WriteLine("Starting listening for incoming messages.");

                var buffer = new byte[8194];

                while (!token.IsCancellationRequested)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead <= 0)
                    {
                        // зʼєднання закрите
                        break;
                    }

                    var data = buffer.AsSpan(0, bytesRead).ToArray();
                    MessageReceived?.Invoke(this, data);
                }
            }
            catch (OperationCanceledException)
            {
                // очікувано при відключенні
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listening loop: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientWrapper));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cts?.Cancel();

            _stream?.Dispose();
            _tcpClient?.Dispose();
            _cts?.Dispose();

            _stream = null;
            _tcpClient = null;
            _cts = null;
        }
    }
}

