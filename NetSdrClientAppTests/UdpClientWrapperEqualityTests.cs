using System;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests.Networking
{
    [TestFixture]
    public class UdpClientWrapperEqualityTests
    {
        [Test]
        public void Equals_SameReference_ReturnsTrue()
        {
            var wrapper = new UdpClientWrapper(60000);

            Assert.That(wrapper.Equals(wrapper), Is.True);
        }

        [Test]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var wrapper = new UdpClientWrapper(60000);

            Assert.That(wrapper.Equals("not a wrapper"), Is.False);
        }

        [Test]
        public void Equals_And_GetHashCode_ForSameEndpoint_AreEqual()
        {
            var a = new UdpClientWrapper(60000);
            var b = new UdpClientWrapper(60000);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_ForDifferentPorts_ReturnsFalse()
        {
            var a = new UdpClientWrapper(60000);
            var b = new UdpClientWrapper(60001);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void StopWithoutStart_IsNoOp()
        {
            var wrapper = new UdpClientWrapper(60000);

            Assert.DoesNotThrow(() => wrapper.StopListening());
            Assert.DoesNotThrow(() => wrapper.Exit());
        }
    }
}

