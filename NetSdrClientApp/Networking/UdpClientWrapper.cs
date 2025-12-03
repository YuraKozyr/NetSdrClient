using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public sealed class UdpClientWrapper : IUdpClient, IDisposable
    {
        private readonly IPEndPoint _localEndPoint;
        private CancellationTokenSource? _cts;
        private UdpClient? _udpClient;
        private bool _disposed;

        public event EventHandler<byte[]>? MessageReceived;

        public UdpClientWrapper(int port)
        {
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
        }

        public async Task StartListeningAsync()
        {
            ThrowIfDisposed();

            if (_cts is not null)
            {
                Console.WriteLine("UDP listener is already running.");
                return;
            }

            _cts = new CancellationTokenSource();
            Console.WriteLine("Start listening for UDP messages...");

            try
            {
                _udpClient = new UdpClient(_localEndPoint);

                while (!_cts.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync(_cts.Token);
                    MessageReceived?.Invoke(this, result.Buffer);
                    Console.WriteLine($"Received from {result.RemoteEndPoint}");
                }
            }
            catch (OperationCanceledException)
            {
                // очікувано при зупинці
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
            finally
            {
                // гарантовано звільняємо ресурси
                DisposeUdpResources();
            }
        }

        public void StopListening()
        {
            StopInternal("StopListening");
        }

        public void Exit()
        {
            StopInternal("Exit");
        }

        private void StopInternal(string reason)
        {
            ThrowIfDisposed();

            try
            {
                if (_cts is null && _udpClient is null)
                {
                    Console.WriteLine($"UDP listener already stopped ({reason}).");
                    return;
                }

                _cts?.Cancel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while cancelling token: {ex.Message}");
            }
            finally
            {
                DisposeUdpResources();
                Console.WriteLine("Stopped listening for UDP messages.");
            }
        }

        private void DisposeUdpResources()
        {
            _cts?.Dispose();
            _udpClient?.Dispose();

            _cts = null;
            _udpClient = null;
        }

        // ---- Рівність та хеш-код без MD5 ----

        public override int GetHashCode()
        {
            // Без криптографії – просто комбінуємо адресу та порт.
            return HashCode.Combine(_localEndPoint.Address, _localEndPoint.Port);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not UdpClientWrapper other)
                return false;

            return Equals(_localEndPoint, other._localEndPoint);
        }

        // ---- Dispose pattern ----

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpClientWrapper));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cts?.Cancel();
            DisposeUdpResources();
        }
    }
}

