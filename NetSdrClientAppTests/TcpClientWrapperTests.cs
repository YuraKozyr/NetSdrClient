using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking
{
    [TestFixture]
    public class TcpClientWrapperTests
    {
        [Test]
        public void Disconnect_WhenNotConnected_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper("localhost", 5555);

            Assert.DoesNotThrow(() => wrapper.Disconnect());
            Assert.False(wrapper.Connected);
        }

        [Test]
        public async Task Connect_And_SendMessage_WorksWithLocalListener()
        {
            // arrange: піднімаємо локальний TcpListener на вільному порту
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            // приймаємо клієнта в бекґраунді
            var acceptTask = listener.AcceptTcpClientAsync();

            // act
            Assert.DoesNotThrow(() => wrapper.Connect());
            Assert.True(wrapper.Connected, "Wrapper should be connected after successful Connect().");

            var message = "hello";
            await wrapper.SendMessageAsync(message);

            // assert: сервер реально щось отримав
            using var serverClient = await acceptTask;
            using var stream = serverClient.GetStream();

            var buffer = new byte[message.Length];
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            var received = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.AreEqual(message, received);

            // cleanup
            wrapper.Disconnect();
            listener.Stop();
        }
    }
}

