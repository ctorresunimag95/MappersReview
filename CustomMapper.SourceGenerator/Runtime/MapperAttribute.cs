using System;

namespace CustomMapper.SourceGenerator.Runtime
{
    /// <summary>
    /// Marks a partial class as a source-generated mapper. The generator implements
    /// every <c>partial</c> mapping method declared on the class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MapperAttribute : Attribute
    {
    }
}
