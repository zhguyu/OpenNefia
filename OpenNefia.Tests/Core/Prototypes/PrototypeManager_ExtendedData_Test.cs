﻿using NUnit.Framework;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager;
using OpenNefia.Core.Serialization.Manager.Attributes;

namespace OpenNefia.Tests.Core.Prototypes
{
    [TestFixture]
    [TestOf(typeof(PrototypeManager))]
    public class PrototypeManager_ExtendedData_Test : OpenNefiaUnitTest
    {
        private IPrototypeManager manager = default!;

        private static readonly PrototypeId<EntityPrototype> TestProto1ID = new("TestProto1");
        private static readonly PrototypeId<EntityPrototype> TestProto2ID = new("TestProto2");
        private static readonly PrototypeId<TilePrototype> TestProto3ID = new("TestProto3");

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            IoCManager.Resolve<ISerializationManager>().Initialize();
            manager = IoCManager.Resolve<IPrototypeManager>();
        }

        [SetUp]
        public void Setup()
        {
            manager.Clear();
            manager.RegisterType<EntityPrototype>();
        }

        [Test]
        public void TestExtendedData()
        {
            var prototypes = @$"
- type: Entity
  id: {TestProto1ID}
  ordering:
    after: {TestProto2ID}
  extendedData:
  - type: TestExtendedData
    foo: 42
    bar: 'baz'

- type: Entity
  id: {TestProto2ID}
";

            manager.LoadString(prototypes);
            manager.ResolveResults();

            Assert.That(manager.HasExtendedData<EntityPrototype, TestExtendedData>(TestProto1ID), Is.True);
            Assert.That(manager.HasExtendedData<EntityPrototype, TestExtendedData>(TestProto2ID), Is.False);

            Assert.That(manager.TryGetExtendedData<EntityPrototype, TestExtendedData>(TestProto1ID, out var data), Is.True);
            Assert.That(data!.Foo, Is.EqualTo(42));
            Assert.That(data.Bar, Is.EqualTo("baz"));
        }

        [Test]
        [Ignore("TODO ability to disable exception tolerance")]
        public void TestExtendedData_InvalidType()
        {
            var prototypes = @$"
- type: Entity
  id: {TestProto1ID}
  extendedData:
  - type: A.B.C.D
";

            Assert.Throws<PrototypeLoadException>(() =>
            {
                manager.LoadString(prototypes);
                manager.ResolveResults();
            }, "Unable to find type ending with");
        }

        [Test]
        public void TestExtendedData_IncompatiblePrototype()
        {
            var prototypes = @$"
- type: Tile
  id: {TestProto3ID}
  extendedData:
  - type: TestExtendedData
    foo: 42
    bar: 'baz'
";

            Assert.Throws<PrototypeLoadException>(() => manager.LoadString(prototypes), "cannot apply to prototype of type");
        }
    }

    [DataDefinition]
    public sealed class TestExtendedData : IPrototypeExtendedData<EntityPrototype>
    {
        [DataField]
        public int Foo { get; set; } = 0;

        [DataField]
        public string Bar { get; set; } = string.Empty;
    }
}
