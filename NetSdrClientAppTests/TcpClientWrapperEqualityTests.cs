using System;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking;

[TestFixture]
public class TcpClientWrapperDisposalTests
{
    [Test]
    public void Disconnect_AfterDispose_ThrowsObjectDisposedException()
    {
        // arrange
        var client = new TcpClientWrapper("127.0.0.1", 12345);

        // act
        client.Dispose();

        // assert
        Assert.Throws<ObjectDisposedException>(() => client.Disconnect());
    }

    [Test]
    public void SendMessageAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // arrange
        var client = new TcpClientWrapper("127.0.0.1", 12345);

        // act
        client.Dispose();

        // assert
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.SendMessageAsync("hello"));
    }
}

