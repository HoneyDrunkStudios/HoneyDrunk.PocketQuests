using System.Reflection;

namespace PocketQuests.SchemaTests.Architecture;

/// <summary>Finds declared and compiled dependencies, including generic shapes and generated async/lambda types.</summary>
internal static class TypeDependencies
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    internal static IReadOnlySet<Type> Read(Type type)
    {
        var dependencies = new HashSet<Type>();
        AddType(type.BaseType, dependencies);
        AddAttributes(type.CustomAttributes, dependencies);
        foreach (var argument in type.GetGenericArguments())
        {
            AddType(argument, dependencies);
        }

        foreach (var contract in type.GetInterfaces())
        {
            AddType(contract, dependencies);
        }

        foreach (var member in type.GetMembers(Declared))
        {
            AddMember(member, dependencies);
            AddAttributes(member.CustomAttributes, dependencies);
            if (member is MethodBase method)
            {
                var body = method.GetMethodBody();
                foreach (var handler in body?.ExceptionHandlingClauses ?? [])
                {
                    if (handler.Flags == ExceptionHandlingClauseOptions.Clause)
                    {
                        AddType(handler.CatchType, dependencies);
                    }
                }

                foreach (var local in body?.LocalVariables ?? [])
                {
                    AddType(local.LocalType, dependencies);
                }

                foreach (var reference in MethodBodyDependencies.Read(method))
                {
                    AddMember(reference, dependencies);
                }
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            dependencies.UnionWith(Read(nested));
        }

        return dependencies;
    }

    private static void AddMember(MemberInfo member, HashSet<Type> dependencies)
    {
        AddType(member.DeclaringType, dependencies);
        switch (member)
        {
            case Type type:
                AddType(type, dependencies);
                break;
            case FieldInfo field:
                AddType(field.FieldType, dependencies);
                break;
            case PropertyInfo property:
                AddType(property.PropertyType, dependencies);
                break;
            case EventInfo eventInfo:
                AddType(eventInfo.EventHandlerType, dependencies);
                break;
            case MethodBase method:
                foreach (var parameter in method.GetParameters())
                {
                    AddType(parameter.ParameterType, dependencies);
                    AddAttributes(parameter.CustomAttributes, dependencies);
                }

                if (method is MethodInfo info)
                {
                    AddType(info.ReturnType, dependencies);
                    AddAttributes(info.ReturnParameter.CustomAttributes, dependencies);
                    foreach (var argument in info.GetGenericArguments())
                    {
                        AddType(argument, dependencies);
                    }
                }

                break;
        }
    }

    private static void AddAttributes(IEnumerable<CustomAttributeData> attributes, HashSet<Type> dependencies)
    {
        foreach (var attribute in attributes)
        {
            AddType(attribute.AttributeType, dependencies);
            foreach (var argument in attribute.ConstructorArguments.Concat(attribute.NamedArguments.Select(argument => argument.TypedValue)))
            {
                AddAttributeArgument(argument, dependencies);
            }
        }
    }

    private static void AddAttributeArgument(CustomAttributeTypedArgument argument, HashSet<Type> dependencies)
    {
        AddType(argument.ArgumentType, dependencies);
        if (argument.Value is Type type)
        {
            AddType(type, dependencies);
        }
        else if (argument.Value is IEnumerable<CustomAttributeTypedArgument> values)
        {
            foreach (var value in values)
            {
                AddAttributeArgument(value, dependencies);
            }
        }
    }

    private static void AddType(Type? type, HashSet<Type> dependencies)
    {
        if (type is null || !dependencies.Add(type))
        {
            return;
        }

        AddType(type.GetElementType(), dependencies);
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                AddType(argument, dependencies);
            }
        }

        if (type.IsGenericParameter)
        {
            foreach (var constraint in type.GetGenericParameterConstraints())
            {
                AddType(constraint, dependencies);
            }
        }
    }
}
