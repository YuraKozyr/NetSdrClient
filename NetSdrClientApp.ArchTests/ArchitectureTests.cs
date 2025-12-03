using NetArchTest.Rules;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientApp.ArchTests;

[TestFixture]
public class ArchitectureTests
{
    /// <summary>
    /// Головна збірка клієнта не повинна залежати від тестового Echo-сервера.
    /// (Echo-сервер – лише допоміжний інструмент для тестування)
    /// </summary>
    [Test]
    public void NetSdrClientApp_Should_Not_Depend_On_EchoTcpServer()
    {
        var result = Types
            .InAssembly(typeof(NetSdrClient).Assembly)
            .ShouldNot()
            .HaveDependencyOn("EchoServer")   // назва збірки EchoTcpServer
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            "NetSdrClientApp не повинен мати залежність від EchoTcpServer (EchoServer).");
    }

    /// <summary>
    /// Повідомлення (Messages) не мають тягнути за собою залежності від мережевого шару.
    /// </summary>
    [Test]
    public void Messages_Should_Not_Depend_On_Networking()
    {
        var result = Types
            .InAssembly(typeof(NetSdrMessageHelper).Assembly)
            .That()
            .ResideInNamespace("NetSdrClientApp.Messages")
            .ShouldNot()
            .HaveDependencyOn("NetSdrClientApp.Networking")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            "NetSdrClientApp.Messages не повинен залежати від NetSdrClientApp.Networking.");
    }

    /// <summary>
    /// Навпаки: мережевий шар не має залежати від Messages,
    /// щоб уникнути циклічних залежностей.
    /// </summary>
    [Test]
    public void Networking_Should_Not_Depend_On_Messages()
    {
        var result = Types
            .InAssembly(typeof(TcpClientWrapper).Assembly)
            .That()
            .ResideInNamespace("NetSdrClientApp.Networking")
            .ShouldNot()
            .HaveDependencyOn("NetSdrClientApp.Messages")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            "NetSdrClientApp.Networking не повинен залежати від NetSdrClientApp.Messages.");
    }

    /// <summary>
    /// Усі типи в просторі імен Networking мають бути *Wrapper*-ами.
    /// </summary>
   [Test]
	 public void Networking_Types_Should_Have_Names_Ending_With_Wrapper()
	 {
		 var result = Types
			 .InAssembly(typeof(TcpClientWrapper).Assembly)
			 .That()
			 .ResideInNamespace("NetSdrClientApp.Networking")
			 .And()
			 .AreClasses()                       // 🔹 важливо: тільки класи, без інтерфейсів
			 .Should()
			 .HaveNameEndingWith("Wrapper")
			 .GetResult();

		 Assert.That(result.IsSuccessful, Is.True,
				 "У NetSdrClientApp.Networking мають бути лише класи, назва яких закінчується на 'Wrapper'.");
	 }
 
}

