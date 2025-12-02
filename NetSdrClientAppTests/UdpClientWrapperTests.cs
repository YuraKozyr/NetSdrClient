using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking
{
    [TestFixture]
    public class UdpClientWrapperTests
    {
			  [Test]
				public void StopListening_WhenNotStarted_DoesNotThrow()
				{
					var wrapper = new UdpClientWrapper(5555);

					Assert.DoesNotThrow(() => wrapper.StopListening());
				}

        [Test]
        public void Exit_WhenNotStarted_DoesNotThrow()
        {
            var wrapper = new UdpClientWrapper(5555);

            Assert.DoesNotThrow(() => wrapper.Exit());
        }

        [Test]
        public void GetHashCode_IsStableForSamePort_AndDifferentForOtherPort()
        {
            var w1 = new UdpClientWrapper(5555);
            var w2 = new UdpClientWrapper(5555);
            var w3 = new UdpClientWrapper(5556);

            var hash1 = w1.GetHashCode();
            var hash2 = w2.GetHashCode();
            var hash3 = w3.GetHashCode();

            Assert.AreEqual(hash1, hash2, "Hash must be stable for однакових параметрів.");
            Assert.AreNotEqual(hash1, hash3, "Hash має відрізнятися для різних портів.");
        }

        [Test]
        public async Task StartListeningAsync_RaisesMessageReceived_WhenPacketArrives()
        {
            // спочатку займаємо вільний UDP порт
            int port;
            using (var probe = new UdpClient(0))
            {
                port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
            }

            var wrapper = new UdpClientWrapper(port);

            var tcs = new TaskCompletionSource<byte[]>();
            wrapper.MessageReceived += (_, data) => tcs.TrySetResult(data);

            var listeningTask = wrapper.StartListeningAsync();

            // відправляємо пакет на цей порт
            using (var sender = new UdpClient())
            {
                var payload = new byte[] { 1, 2, 3 };
                await sender.SendAsync(payload, payload.Length, "127.0.0.1", port);

                // чекаємо максимум 1 секунду
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
                Assert.AreSame(tcs.Task, completed, "Повідомлення не було отримано за таймаут.");

                CollectionAssert.AreEqual(payload, tcs.Task.Result);
            }

            wrapper.StopListening();

            // даємо задачі акуратно завершитися, але не блокуємось вічно
            await Task.WhenAny(listeningTask, Task.Delay(1000));
        }
    }
}

