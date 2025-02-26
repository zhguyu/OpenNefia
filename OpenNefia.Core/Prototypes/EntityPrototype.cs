using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Log;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Serialization;
using OpenNefia.Core.Serialization.Manager;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Core.Serialization.Markdown.Mapping;
using OpenNefia.Core.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using OpenNefia.Core.ViewVariables;

namespace OpenNefia.Core.Prototypes
{
    /// <summary>
    /// Prototype that represents game entities.
    /// </summary>
    [Prototype("Entity", -1)]
    public class EntityPrototype : IPrototype, IInheritingPrototype, ISerializationHooks, IHspIds<int>
    {
        /// <summary>
        /// The "in code name" of the object. Must be unique.
        /// </summary>
        [IdDataField]
        public string ID { get; private set; } = default!;

        /// <summary>
        /// Type identifier for the corresponding entity in HSP variants of Elona.
        /// 
        /// Since all types of entities (character, item, feat, mef) are consolidated
        /// under <see cref="EntityPrototype"/>, it becomes necessary to state both what
        /// the "logical" type ("chara", "item") and the numeric ID in <see cref="HspIds"/> are.
        /// </summary>
        /// <seealso cref="HspEntityTypes"/>
        [DataField]
        public string? HspEntityType { get; }

        /// <inheritdoc/>
        [DataField]
        [NeverPushInheritance]
        public HspIds<int>? HspIds { get; }

        /// <summary>
        /// Cell object IDs of this entity for each HSP variant.
        /// This is used during HSP map conversion.
        /// </summary>
        [DataField("hspCellObjIds")]
        [NeverPushInheritance]
        private readonly Dictionary<string, HashSet<int>> _hspCellObjIds = new();
        public IReadOnlyDictionary<string, HashSet<int>> HspCellObjIds => _hspCellObjIds;

        /// <summary>
        ///     If true, this prototype should only be inherited from.
        /// </summary>
        [NeverPushInheritance]
        [AbstractDataField]
        public bool Abstract { get; }

        /// <summary>
        ///     If true, this object should not show up in the entity spawn panel.
        /// </summary>
        [NeverPushInheritance]
        [DataField("noSpawn")]
        public bool NoSpawn { get; private set; }

        /// True if this entity will be saved by the map loader.
        /// </summary>
        [DataField]
        public bool MapSavable { get; protected set; } = true;

        /// <summary>
        /// The prototypes we inherit from.
        /// </summary>
        [ViewVariables]
        [ParentDataField(typeof(PrototypeIdArraySerializer<EntityPrototype>))]
        public string[]? Parents { get; }

        /// <summary>
        /// A dictionary mapping the component type list to the YAML mapping containing their settings.
        /// </summary>
        [DataField]
        [AlwaysPushInheritance]
        public ComponentRegistry Components { get; } = new();

        public EntityPrototype()
        {
            // All entities come with a spatial and metadata component.
            Components.Add("Spatial", new ComponentRegistryEntry(new SpatialComponent(), new MappingDataNode()));
            Components.Add("MetaData", new ComponentRegistryEntry(new MetaDataComponent(), new MappingDataNode()));
        }

        public override string ToString()
        {
            return $"EntityPrototype({ID})";
        }

        public class ComponentRegistry : Dictionary<string, ComponentRegistryEntry>
        {
            public ComponentRegistry()
            {
            }

            public ComponentRegistry(Dictionary<string, ComponentRegistryEntry> components) : base(components)
            {
            }

            public bool HasComponent<T>(IComponentFactory? compFac = null)
                where T : class, IComponent
            {
                IoCManager.Resolve(ref compFac);
                var compName = compFac.GetComponentName<T>();

                if (!TryGetValue(compName, out var compEntry))
                    return false;

                return compEntry.Component is T;
            }

            public bool TryGetComponent<T>([NotNullWhen(true)] out T? component, IComponentFactory? compFac = null)
                where T : class, IComponent
            {
                IoCManager.Resolve(ref compFac);
                var compName = compFac.GetComponentName<T>();

                if (TryGetValue(compName, out var compEntry) && compEntry.Component is T)
                {
                    component = (compEntry.Component as T)!;
                    return true;
                }

                component = null;
                return false;
            }

            public T GetComponent<T>(IComponentFactory? compFac = null)
                where T : class, IComponent
            {
                IoCManager.Resolve(ref compFac);
                var compName = compFac.GetComponentName<T>();

                if (this[compName].Component is not T component)
                    throw new InvalidDataException($"Component of type {compName} not found.");

                return component;
            }
        }

        public sealed class ComponentRegistryEntry
        {
            public readonly IComponent Component;
            // Mapping is just a quick reference to speed up entity creation.
            public readonly MappingDataNode Mapping;

            public ComponentRegistryEntry(IComponent component, MappingDataNode mapping)
            {
                Component = component;
                Mapping = mapping;
            }
        }
    }
}
