using System;

namespace MxFramework.Resources
{
    public readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public ResourceKey(string id, string typeId, string variant = "", string packageId = "")
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            Variant = variant ?? string.Empty;
            PackageId = packageId ?? string.Empty;
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Variant { get; }
        public string PackageId { get; }
        public bool IsValid => IsValidId(Id) && !string.IsNullOrWhiteSpace(TypeId);

        public ResourceKey WithoutPackage()
        {
            return new ResourceKey(Id, TypeId, Variant);
        }

        public ResourceKey WithPackage(string packageId)
        {
            return new ResourceKey(Id, TypeId, Variant, packageId);
        }

        public bool Equals(ResourceKey other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(TypeId, other.TypeId, StringComparison.Ordinal)
                && string.Equals(Variant, other.Variant, StringComparison.Ordinal)
                && string.Equals(PackageId, other.PackageId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(TypeId ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Variant ?? string.Empty);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(PackageId ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            string variant = string.IsNullOrEmpty(Variant) ? string.Empty : "#" + Variant;
            string package = string.IsNullOrEmpty(PackageId) ? string.Empty : "@" + PackageId;
            return Id + ":" + TypeId + variant + package;
        }

        public static bool operator ==(ResourceKey left, ResourceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ResourceKey left, ResourceKey right)
        {
            return !left.Equals(right);
        }

        public static bool IsValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-';
                if (!valid)
                    return false;
            }

            return true;
        }
    }
}
