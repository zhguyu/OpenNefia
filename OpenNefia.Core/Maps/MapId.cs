using OpenNefia.Core.SaveGames;

namespace OpenNefia.Core.Maps
{
    [Serializable]
    public readonly struct MapId : IEquatable<MapId>
    {
        /// <summary>
        /// Global entity storage. This is used for things like areas that are not attached 
        /// to any map, but should be available no matter which map the player is in.
        /// </summary>
        /// <remarks>
        /// This map will be saved when the global session/serialized data is saved,
        /// and will be loaded automatically when the game is loaded.
        /// (see <see cref="ISaveGameSerializer"/>)
        /// </remarks>
        public static readonly MapId Global = new(-1);

        /// <summary>
        /// An invalid map. This map is never loaded and entities cannot be spawned there.
        /// </summary>
        public static readonly MapId Nullspace = new(0);

        public static readonly MapId FirstId = new(1);

        internal readonly int Value;

        public MapId(int value)
        {
            Value = value;
        }

        /// <inheritdoc />
        public bool Equals(MapId other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MapId id && Equals(id);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(MapId a, MapId b)
        {
            return a.Value == b.Value;
        }

        public static bool operator !=(MapId a, MapId b)
        {
            return !(a == b);
        }

        public static explicit operator int(MapId self)
        {
            return self.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
