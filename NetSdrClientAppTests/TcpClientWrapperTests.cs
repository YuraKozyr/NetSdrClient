using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        private const string Host = "127.0.0.1";

        // ----------------- helpers -----------------

        private static void SetPrivateField<T>(TcpClientWrapper wrapper, string fieldName, T? value)
        {
            var field = typeof(TcpClientWrapper).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Field '{fieldName}' was not found via reflection.");
            field!.SetValue(wrapper, value);
        }

        private static Task InvokeStartListeningAsync(TcpClientWrapper wrapper, CancellationToken token)
        {
            var method = typeof(TcpClientWrapper).GetMethod("StartListeningAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "StartListeningAsync not found via reflection.");

            var taskObj = method!.Invoke(wrapper, new object[] { token });
            return (Task)taskObj!;
        }

        /// <summary>
        /// Стрім, який кидає в Close(), але мовчить у Dispose().
        /// Використовується, щоб зайти в catch у Disconnect.
        /// </summary>
        private sealed class ThrowOnCloseNetworkStream : NetworkStream
        {
            public ThrowOnCloseNetworkStream(Socket socket)
                : base(socket, FileAccess.ReadWrite, ownsSocket: false)
            {
            }

            public override void Close()
            {
                throw new InvalidOperationException("Close boom");
            }

            protected override void Dispose(bool disposing)
            {
                // спеціально нічого не робимо, щоб finally у Disconnect не впав
            }
        }

        /// <summary>
        /// Стрім, який кидає в ReadAsync, але мовчить у Dispose().
        /// Використовується, щоб зайти в catch (Exception ex) у StartListeningAsync.
        /// </summary>
        private sealed class ThrowOnReadNetworkStream : NetworkStream
        {
            public ThrowOnReadNetworkStream(Socket socket)
                : base(socket, FileAccess.ReadWrite, ownsSocket: false)
            {
            }

            public override bool CanRead => true;

            public override Task<int> ReadAsync(
                byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("ReadAsync boom");
            }

            protected override void Dispose(bool disposing)
            {
                // глушимо, щоб finally у StartListeningAsync завершився без винятку
            }
        }

        // ----------------- базові сценарії Connect / Send / Disconnect -----------------

        [Test]
        public void Connect_WhenServerIsUnavailable_DoesNotThrow_AndStaysDisconnected()
        {
            var wrapper = new TcpClientWrapper(Host, 1);

            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.That(wrapper.Connected, Is.False);
        }

        [Test]
        public async Task Connect_And_SendMessage_WorksWithLocalListener()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var wrapper = new TcpClientWrapper(Host, port);

            var acceptTask = listener.AcceptTcpClientAsync();

            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.True(wrapper.Connected, "Wrapper should be connected after Connect().");

            const string message = "hello";
            await wrapper.SendMessageAsync(message);

            using var serverClient = await acceptTask;
            using var stream = serverClient.GetStream();

            var buffer = new byte[message.Length];
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            var received = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.That(received, Is.EqualTo(message));

            wrapper.Disconnect();
            listener.Stop();
        }

        [Test]
        public void Connect_WhenAlreadyConnected_DoesNotThrow_AndStaysConnected()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var wrapper = new TcpClientWrapper(Host, port);
            var acceptTask = listener.AcceptTcpClientAsync();

            wrapper.Connect();
            using var _ = acceptTask.Result; // лише для встановлення конекта

            Assert.IsTrue(wrapper.Connected, "First connect failed.");

            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.IsTrue(wrapper.Connected, "After second Connect() we still must be connected.");

            wrapper.Disconnect();
            listener.Stop();
        }

        [Test]
        public void Disconnect_WhenNotConnected_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper("localhost", 5555);

            Assert.DoesNotThrow(() => wrapper.Disconnect());
            Assert.False(wrapper.Connected);
        }

        [Test]
        public async Task Dispose_WhenConnected_ReleasesResourcesAndDisconnects()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();
            var wrapper = new TcpClientWrapper(Host, port);

            wrapper.Connect();
            using var _ = await acceptTask;

            Assert.That(wrapper.Connected, Is.True);

            wrapper.Dispose();

            Assert.That(wrapper.Connected, Is.False);

            listener.Stop();
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper(Host, 12345);

            wrapper.Dispose();
            Assert.DoesNotThrow(() => wrapper.Dispose());
        }

        [Test]
        public void SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
        {
            var wrapper = new TcpClientWrapper(Host, 65000);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync("hello"));

            Assert.That(ex!.Message, Is.EqualTo("Not connected to a server."));
        }

        // ----------------- ThrowIfDisposed -----------------

        [Test]
        public void Connect_AfterDispose_ThrowsObjectDisposedException()
        {
            var wrapper = new TcpClientWrapper(Host, 12345);
            wrapper.Dispose();

            var ex = Assert.Throws<ObjectDisposedException>(() => wrapper.Connect());
            Assert.That(ex!.ObjectName, Is.EqualTo(nameof(TcpClientWrapper)));
        }

        [Test]
        public void Disconnect_AfterDispose_ThrowsObjectDisposedException()
        {
            var wrapper = new TcpClientWrapper(Host, 12345);
            wrapper.Dispose();

            Assert.Throws<ObjectDisposedException>(() => wrapper.Disconnect());
        }

        [Test]
        public void SendMessageAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var wrapper = new TcpClientWrapper(Host, 12345);
            wrapper.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await wrapper.SendMessageAsync("hello"));
        }

        // ----------------- Connect: стара CTS -----------------

        [Test]
        public void Connect_WhenOldCancellationTokenSourceExists_CancelsAndDisposesIt()
        {
            var wrapper = new TcpClientWrapper(Host, 1);

            var oldCts = new CancellationTokenSource();
            SetPrivateField(wrapper, "_cts", oldCts);

            Assert.DoesNotThrow(() => wrapper.Connect());

            Assert.That(oldCts.IsCancellationRequested, Is.True,
                "Old CTS must be cancelled when Connect() is called again.");
        }

        // ----------------- Disconnect: catch (Exception ex) -----------------

        [Test]
        public async Task Disconnect_WhenStreamCloseThrows_ThrowsInvalidOperationException()
        {
            // реальне з’єднання, щоб TcpClient.Connected == true
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var client = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(Host, port);
            using var _ = await acceptTask; // тримаємо серверний бік відкритим

            var wrapper = new TcpClientWrapper("dummy", 0);

            var throwingStream = new ThrowOnCloseNetworkStream(client.Client);

            SetPrivateField(wrapper, "_tcpClient", client);
            SetPrivateField(wrapper, "_stream", throwingStream);
            SetPrivateField(wrapper, "_cts", new CancellationTokenSource());

            Assert.That(wrapper.Connected, Is.True, "Precondition: wrapper must be logically connected.");

            var ex = Assert.Throws<InvalidOperationException>(() => wrapper.Disconnect());
            Assert.That(ex!.Message, Is.EqualTo("Close boom"));

            listener.Stop();
        }

        // ----------------- StartListeningAsync: успішний сценарій (MessageReceived) -----------------

        [Test]
        public async Task MessageReceived_Event_Raised_WhenServerSendsData()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var wrapper = new TcpClientWrapper(Host, port);

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            wrapper.MessageReceived += (sender, data) =>
            {
                tcs.TrySetResult(data);
            };

            var acceptTask = listener.AcceptTcpClientAsync();

            // Connect() всередині запустить StartListeningAsync(_cts.Token)
            wrapper.Connect();

            using var serverClient = await acceptTask;
            using var serverStream = serverClient.GetStream();

            var msgBytes = Encoding.UTF8.GetBytes("ping");
            await serverStream.WriteAsync(msgBytes, 0, msgBytes.Length);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.That(completed, Is.SameAs(tcs.Task), "MessageReceived was not raised in time.");

            var received = tcs.Task.Result;
            Assert.That(Encoding.UTF8.GetString(received), Is.EqualTo("ping"));

            wrapper.Disconnect();
            listener.Stop();
        }

        // ----------------- StartListeningAsync: catch (Exception ex) -----------------

        [Test]
        public async Task StartListeningAsync_WhenReadAsyncThrows_DoesNotPropagateException()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var client = new TcpClient();
            var acceptTask = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(Host, port);
            using var _ = await acceptTask;

            var wrapper = new TcpClientWrapper("dummy", 0);

            var throwingStream = new ThrowOnReadNetworkStream(client.Client);

            SetPrivateField(wrapper, "_tcpClient", client);
            SetPrivateField(wrapper, "_stream", throwingStream);

            var listenTask = InvokeStartListeningAsync(wrapper, CancellationToken.None);

            await listenTask;

            listener.Stop();
        }
    }
}

