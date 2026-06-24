using System;

namespace CustomMapper.SourceGenerator.Runtime
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MapperIgnoreAttribute : Attribute
    {
        public MapperIgnoreAttribute(Type destinationType, params string[] propertyNames)
        {
        }
    }
}