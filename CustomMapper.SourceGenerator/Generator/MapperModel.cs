using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CustomMapper.SourceGenerator.Generator
{
    /// <summary>
    /// A single destination = source property assignment, resolved during transform.
    /// </summary>
    internal readonly struct PropertyAssignment : IEquatable<PropertyAssignment>
    {
        public PropertyAssignment(string destinationProperty, string sourceProperty)
        {
            DestinationProperty = destinationProperty;
            SourceProperty = sourceProperty;
        }

        public string DestinationProperty { get; }
        public string SourceProperty { get; }

        public bool Equals(PropertyAssignment other) =>
            DestinationProperty == other.DestinationProperty && SourceProperty == other.SourceProperty;

        public override bool Equals(object? obj) => obj is PropertyAssignment other && Equals(other);

        public override int GetHashCode() =>
            unchecked((DestinationProperty.GetHashCode() * 397) ^ SourceProperty.GetHashCode());
    }

    /// <summary>
    /// Equatable description of one generated mapping method.
    /// Holds only strings + an equatable list, never symbols/syntax/compilations.
    /// </summary>
    internal sealed class MapMethodModel : IEquatable<MapMethodModel>
    {
        public MapMethodModel(
            string methodName,
            string sourceTypeGlobal,
            string destinationTypeGlobal,
            bool hasExtendMap,
            EquatableArray<PropertyAssignment> assignments)
        {
            MethodName = methodName;
            SourceTypeGlobal = sourceTypeGlobal;
            DestinationTypeGlobal = destinationTypeGlobal;
            HasExtendMap = hasExtendMap;
            Assignments = assignments;
        }

        public string MethodName { get; }
        public string SourceTypeGlobal { get; }
        public string DestinationTypeGlobal { get; }
        public bool HasExtendMap { get; }
        public EquatableArray<PropertyAssignment> Assignments { get; }

        public bool Equals(MapMethodModel? other)
        {
            if (other is null) return false;
            return MethodName == other.MethodName
                && SourceTypeGlobal == other.SourceTypeGlobal
                && DestinationTypeGlobal == other.DestinationTypeGlobal
                && HasExtendMap == other.HasExtendMap
                && Assignments.Equals(other.Assignments);
        }

        public override bool Equals(object? obj) => Equals(obj as MapMethodModel);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MethodName.GetHashCode();
                hash = (hash * 397) ^ SourceTypeGlobal.GetHashCode();
                hash = (hash * 397) ^ DestinationTypeGlobal.GetHashCode();
                hash = (hash * 397) ^ HasExtendMap.GetHashCode();
                hash = (hash * 397) ^ Assignments.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Equatable description of one <c>[Mapper]</c> class and its mapping methods.
    /// </summary>
    internal sealed class MapperClassModel : IEquatable<MapperClassModel>
    {
        public MapperClassModel(
            string? namespaceName,
            string className,
            string classAccessibility,
            EquatableArray<MapMethodModel> methods)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            ClassAccessibility = classAccessibility;
            Methods = methods;
        }

        public string? NamespaceName { get; }
        public string ClassName { get; }
        public string ClassAccessibility { get; }
        public EquatableArray<MapMethodModel> Methods { get; }

        public bool Equals(MapperClassModel? other)
        {
            if (other is null) return false;
            return NamespaceName == other.NamespaceName
                && ClassName == other.ClassName
                && ClassAccessibility == other.ClassAccessibility
                && Methods.Equals(other.Methods);
        }

        public override bool Equals(object? obj) => Equals(obj as MapperClassModel);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = NamespaceName?.GetHashCode() ?? 0;
                hash = (hash * 397) ^ ClassName.GetHashCode();
                hash = (hash * 397) ^ ClassAccessibility.GetHashCode();
                hash = (hash * 397) ^ Methods.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// A value-equality wrapper around an immutable array, so models stored in the
    /// incremental pipeline compare by content (required for caching to work).
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array) => _array = array;

        public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? [] : _array;

        public int Length => _array.IsDefault ? 0 : _array.Length;

        public bool Equals(EquatableArray<T> other)
        {
            var left = AsImmutableArray();
            var right = other.AsImmutableArray();
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var item in AsImmutableArray())
                {
                    hash = (hash * 397) ^ (item?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }

    internal static class EquatableArrayExtensions
    {
        public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
            where T : IEquatable<T>
            => new([.. source]);
    }
}
