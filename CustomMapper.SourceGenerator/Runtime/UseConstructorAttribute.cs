using System;

namespace CustomMapper.SourceGenerator.Runtime
{
    /// <summary>
    /// Applied to a partial mapping method to request constructor-based mapping
    /// for the destination type. The generator selects the public constructor
    /// whose parameters best match source properties by name and type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UseConstructorAttribute : Attribute
    {
    }
}
