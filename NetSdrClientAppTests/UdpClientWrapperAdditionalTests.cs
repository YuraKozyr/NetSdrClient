using System;
using System.Threading.Tasks;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking;

[TestFixture]
public class UdpClientWrapperAdditionalTests
{
    [Test]
    public void StartListeningAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // arrange
        var client = new UdpClientWrapper(0); // порт не важливий, ми не дійдемо до створення UdpClient

        client.Dispose();

        // assert
        Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.StartListeningAsync());
    }

    [Test]
    public void Equals_And_GetHashCode_Work_ForSameAndDifferentPorts()
    {
        // arrange
        var a = new UdpClientWrapper(12345);
        var b = new UdpClientWrapper(12345);
        var c = new UdpClientWrapper(54321);

        // act + assert
        Assert.That(a.Equals(b), Is.True, "Wrapper-и з однаковим портом мають бути рівні");
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()),
            "HashCode для однакових параметрів має співпадати");

        Assert.That(a.Equals(c), Is.False, "Wrapper-и з різними портами не повинні бути рівні");
    }
}

