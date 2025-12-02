using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>((bytes) =>
                {
                    _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
                });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        await _client.ConnectAsync();

        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        _client.Disconect();

        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        await ConnectAsyncTest();

        _client.Disconect();

        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        await _client.StartIQAsync();

        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        await ConnectAsyncTest();

        await _client.StartIQAsync();

        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        await ConnectAsyncTest();

        await _client.StopIQAsync();

        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StopIQAsync_WhenNotConnected_DoesNotThrow()
    {
        var tcpMock = new Mock<ITcpClient>();
        tcpMock.SetupGet(t => t.Connected).Returns(false);

        var udpMock = new Mock<IUdpClient>();

        var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

        Assert.DoesNotThrowAsync(async () => await client.StopIQAsync());
    }

    [Test]
    public async Task ChangeFrequencyAsync_WhenNotConnected_DoesNotThrowAndUsesSendTcpRequestBranch()
    {
        var tcpMock = new Mock<ITcpClient>();
        tcpMock.SetupGet(t => t.Connected).Returns(false);

        var udpMock = new Mock<IUdpClient>();

        var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

        Assert.DoesNotThrowAsync(async () => await client.ChangeFrequencyAsync(20_000_000, 1));
    }

    [Test]
    public void UdpClient_MessageReceived_WritesSamplesToFile()
    {
        var tcpMock = new Mock<ITcpClient>();
        tcpMock.SetupGet(t => t.Connected).Returns(true);

        var udpMock = new Mock<IUdpClient>();

        EventHandler<byte[]>? handler = null;

        udpMock.SetupAdd(u => u.MessageReceived += It.IsAny<EventHandler<byte[]>>())
               .Callback<EventHandler<byte[]>>(h => handler += h);

        udpMock.SetupRemove(u => u.MessageReceived -= It.IsAny<EventHandler<byte[]>>())
               .Callback<EventHandler<byte[]>>(h => handler -= h);

        var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

        const string fileName = "samples.bin";
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }

        var body = new byte[] { 0x00, 0x01, 0x00, 0x02 }; // два 16-бітні семпли
        var msg = NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem0, body);

        // емулюємо прихід UDP-повідомлення
        handler?.Invoke(this, msg);

        Assert.That(File.Exists(fileName), Is.True, "Файл samples.bin мав бути створений");

        var length = new FileInfo(fileName).Length;
        Assert.That(length, Is.GreaterThan(0), "Файл samples.bin має містити дані");

        File.Delete(fileName);
    }
}

