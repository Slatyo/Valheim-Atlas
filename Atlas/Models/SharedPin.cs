using System;
using UnityEngine;

// Valheim types are in the global namespace
// ZPackage, Minimap are from assembly_valheim.dll

namespace Atlas.Models
{
    /// <summary>
    /// Represents a shared map pin that can be synchronized between players
    /// </summary>
    [Serializable]
    public class SharedPin
    {
        /// <summary>
        /// Unique identifier for this pin (GUID)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// World position of the pin
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Type of pin (Icon, Death, Bed, etc.)
        /// </summary>
        public Minimap.PinType Type { get; set; }

        /// <summary>
        /// Display name of the pin
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether the pin is checked/crossed out
        /// </summary>
        public bool Checked { get; set; }

        /// <summary>
        /// Player ID who created this pin
        /// </summary>
        public long CreatedBy { get; set; }

        /// <summary>
        /// Display name of the player who created this pin
        /// </summary>
        public string CreatorName { get; set; }

        /// <summary>
        /// Unix timestamp when the pin was created
        /// </summary>
        public long CreatedAt { get; set; }

        /// <summary>
        /// Unix timestamp when the pin was last modified
        /// </summary>
        public long ModifiedAt { get; set; }

        public SharedPin()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ModifiedAt = CreatedAt;
        }

        public SharedPin(Minimap.PinData pinData, long playerId, string playerName) : this()
        {
            Position = pinData.m_pos;
            Type = pinData.m_type;
            Name = pinData.m_name ?? "";
            Checked = pinData.m_checked;
            CreatedBy = playerId;
            CreatorName = playerName ?? "Unknown";
        }

        /// <summary>
        /// Serialize the pin to a ZPackage for network transfer
        /// </summary>
        public void Serialize(ZPackage pkg)
        {
            pkg.Write(Id);
            pkg.Write(Position);
            pkg.Write((int)Type);
            pkg.Write(Name ?? "");
            pkg.Write(Checked);
            pkg.Write(CreatedBy);
            pkg.Write(CreatorName ?? "");
            pkg.Write(CreatedAt);
            pkg.Write(ModifiedAt);
        }

        /// <summary>
        /// Deserialize a pin from a ZPackage
        /// </summary>
        public static SharedPin Deserialize(ZPackage pkg)
        {
            return new SharedPin
            {
                Id = pkg.ReadString(),
                Position = pkg.ReadVector3(),
                Type = (Minimap.PinType)pkg.ReadInt(),
                Name = pkg.ReadString(),
                Checked = pkg.ReadBool(),
                CreatedBy = pkg.ReadLong(),
                CreatorName = pkg.ReadString(),
                CreatedAt = pkg.ReadLong(),
                ModifiedAt = pkg.ReadLong()
            };
        }

        /// <summary>
        /// Check if this pin is a duplicate of another (same type, name, and close position)
        /// </summary>
        public bool IsDuplicateOf(SharedPin other, float distanceThreshold = 25f)
        {
            if (other == null) return false;
            if (Type != other.Type) return false;
            if (!string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)) return false;

            float distance = Vector3.Distance(Position, other.Position);
            return distance < distanceThreshold;
        }

        /// <summary>
        /// Check if this pin matches a vanilla PinData
        /// </summary>
        public bool MatchesPinData(Minimap.PinData pinData, float distanceThreshold = 1f)
        {
            if (pinData == null) return false;
            if (Type != pinData.m_type) return false;

            float distance = Vector3.Distance(Position, pinData.m_pos);
            if (distance > distanceThreshold) return false;

            // Name can be slightly different due to encoding, so be lenient
            return string.Equals(Name, pinData.m_name ?? "", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return $"SharedPin[{Id}] {Type} '{Name}' at {Position} by {CreatorName}";
        }
    }
}
