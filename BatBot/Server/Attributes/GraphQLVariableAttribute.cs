using System;

namespace BatBot.Server.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class GraphQLVariableAttribute : Attribute
    {
        public GraphQLVariableAttribute(string typeName) => TypeName = typeName;

        public string TypeName { get; }
    }
}
