﻿using JetBrains.Annotations;
using NUnit.Framework;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Core.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace OpenNefia.Tests.Core.Prototypes
{
    [UsedImplicitly]
    [TestFixture]
    public class PrototypeManager_Test : OpenNefiaUnitTest
    {
        private static PrototypeId<EntityPrototype> LoadStringTestDummyId = new("LoadStringTestDummy");
        private IPrototypeManager manager = default!;

        [OneTimeSetUp]
        public void Setup()
        {
            var factory = IoCManager.Resolve<IComponentFactory>();
            factory.RegisterClass<TestBasicPrototypeComponent>();

            IoCManager.Resolve<ISerializationManager>().Initialize();
            manager = IoCManager.Resolve<IPrototypeManager>();
            manager.RegisterType<EntityPrototype>();
            manager.LoadString(DOCUMENT);
            manager.ResolveResults();
        }

        [Test]
        public void TestBasicPrototype()
        {
            var prototype = manager.Index<EntityPrototype>(new("wrench"));

            var mapping = prototype.Components["TestBasicPrototypeComponent"].Component as TestBasicPrototypeComponent;
            Assert.That(mapping!.Foo, Is.EqualTo("bar!"));
        }

        [Test]
        public void TestYamlHelpersPrototype()
        {
            var prototype = manager.Index<EntityPrototype>(new("yamltester"));
            Assert.That(prototype.Components, Contains.Key("TestBasicPrototypeComponent"));

            var componentData = prototype.Components["TestBasicPrototypeComponent"].Component as TestBasicPrototypeComponent;

            Assert.NotNull(componentData);
            Assert.That(componentData!.Str, Is.EqualTo("hi!"));
            Assert.That(componentData!.int_field, Is.EqualTo(10));
            Assert.That(componentData!.float_field, Is.EqualTo(10f));
            Assert.That(componentData!.float2_field, Is.EqualTo(10.5f));
            Assert.That(componentData!.boolt, Is.EqualTo(true));
            Assert.That(componentData!.boolf, Is.EqualTo(false));
            Assert.That(componentData!.vec2, Is.EqualTo(new Vector2(1.5f, 1.5f)));
            Assert.That(componentData!.vec2i, Is.EqualTo(new Vector2i(1, 1)));
            Assert.That(componentData!.vec3, Is.EqualTo(new Vector3(1.5f, 1.5f, 1.5f)));
            Assert.That(componentData!.vec4, Is.EqualTo(new Vector4(1.5f, 1.5f, 1.5f, 1.5f)));
            Assert.That(componentData!.color, Is.EqualTo(new Color(0xAA, 0xBB, 0xCC, 0xFF)));
            Assert.That(componentData!.enumf, Is.EqualTo(YamlTestEnum.Foo));
            Assert.That(componentData!.enumb, Is.EqualTo(YamlTestEnum.Bar));
        }

        [Test]
        public void TestYamlHelpers2Prototype()
        {
            var prototype = manager.Index<EntityPrototype>(new("yamltester2"));
            Assert.That(prototype.Components, Contains.Key("TestBasicPrototypeComponent"));

            var componentData = prototype.Components["TestBasicPrototypeComponent"].Component as TestBasicPrototypeComponent;

            Assert.NotNull(componentData);
            Assert.That(componentData!.int_field, Is.Null);
            Assert.That(componentData!.float_field, Is.Null);
            Assert.That(componentData!.float2_field, Is.Null);
            Assert.That(componentData!.boolt, Is.Null);
            Assert.That(componentData!.boolf, Is.Null);
        }

        [Test]
        public void TestLoadString()
        {
            manager.LoadString(LoadStringDocument);
            manager.ResolveResults();

            var prototype = manager.Index<EntityPrototype>(LoadStringTestDummyId);

            Assert.That(prototype.GetStrongID(), Is.EqualTo(LoadStringTestDummyId));
        }

        [Test]
        public void TestLoadCircular()
        {
            var document = $@"
- type: Entity
  id: circular
  parent: circular
  abstract: true
";

            Assert.Throws<PrototypeLoadException>(() =>
            {
                manager.LoadString(document);
                manager.ResolveResults();
            });
        }

        [Test]
        public void TestCircleException()
        {
            string GenerateCircleTestPrototype(string id, string parent)
            {
                return $@"- type: circle
  id: {id}
  parent: {parent}";
            }

            manager.RegisterType(typeof(CircleTestPrototype));

            var directCircle = $@"{GenerateCircleTestPrototype("1", "2")}
{GenerateCircleTestPrototype("2", "1")}";

            Assert.Throws<PrototypeLoadException>(() => manager.LoadString(directCircle));
            manager.RemoveString(directCircle);

            var indirectCircle = $@"{GenerateCircleTestPrototype("1", "2")}
{GenerateCircleTestPrototype("2", "3")}
{GenerateCircleTestPrototype("3", "1")}";

            Assert.Throws<PrototypeLoadException>(() => manager.LoadString(indirectCircle));
        }

        [Prototype("circle")]
        private sealed class CircleTestPrototype : IPrototype, IInheritingPrototype
        {
            [IdDataField()]
            public string ID { get; } = default!;
            [ParentDataField(typeof(PrototypeIdArraySerializer<CircleTestPrototype>))]
            public string[]? Parents { get; }
            [AbstractDataField]
            public bool Abstract { get; }
        }

        public enum YamlTestEnum : byte
        {
            Foo,
            Bar
        }

        const string DOCUMENT = @"
- type: Entity
  id: wrench
  components:
  - type: TestBasicPrototypeComponent
    foo: bar!

- type: Entity
  id: wallLight
  components:
  - type: Spatial
  - type: Sprite
  - type: PointLight

- type: Entity
  id: wallLightChild
  parents: wallLight

- type: Entity
  id: yamltester
  components:
  - type: TestBasicPrototypeComponent
    str: hi!
    int: 10
    float: 10
    float2: 10.5
    boolt: true
    boolf: false
    vec2: 1.5, 1.5
    vec2i: 1, 1
    vec3: 1.5, 1.5, 1.5
    vec4: 1.5, 1.5, 1.5, 1.5
    color: '#aabbcc'
    enumf: Foo
    enumb: Bar

- type: Entity
  id: yamltester2
  components:
  - type: TestBasicPrototypeComponent
    int: null
    float: null
    float2: null
    boolt: null
    boolf: null
";

        private static readonly string LoadStringDocument = $@"
- type: Entity
  id: {LoadStringTestDummyId}";
    }

    [ComponentProtoName("TestBasicPrototypeComponent")]
    public class TestBasicPrototypeComponent : Component
    {
        [DataField("foo")] public string Foo = null!;

        [DataField("str")] public string Str = null!;

        [DataField("int")] public int? int_field = null!;

        [DataField("float")] public float? float_field = null!;

        [DataField("float2")] public float? float2_field = null!;

        [DataField("boolt")] public bool? @boolt = null!;

        [DataField("boolf")] public bool? @boolf = null!;

        [DataField("vec2")] public Vector2 vec2 = default;

        [DataField("vec2i")] public Vector2i vec2i = default;

        [DataField("vec3")] public Vector3 vec3 = default;

        [DataField("vec4")] public Vector4 vec4 = default;

        [DataField("color")] public Color color = default;

        [DataField("enumf")] public PrototypeManager_Test.YamlTestEnum enumf = default;

        [DataField("enumb")] public PrototypeManager_Test.YamlTestEnum enumb = default;
    }
}
