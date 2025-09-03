using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNetClone;

public static class ReflectionExtensions
{
    public static bool HasAttribute<T>(this MemberInfo member) where T : Attribute
        => member.GetCustomAttribute<T>() != null;

    public static bool HasAttribute<T>(this MemberInfo member, bool inherit) where T : Attribute
        => member.GetCustomAttribute<T>(inherit) != null;

    public static bool TryGetAttribute<T>(
        this MemberInfo member,
        [NotNullWhen(true)] out T? attribute,
        bool inherit = true
    ) where T : Attribute
    {
        attribute = member.GetCustomAttribute<T>(inherit);
        return attribute != null;
    }
    
    public static bool TryGetGenericTypeImplementation(
        this Type type,
        Type genericInterfaceDefinition,
        [NotNullWhen(true)] out Type? implementingType)
    {
        if (!genericInterfaceDefinition.IsInterface || !genericInterfaceDefinition.IsGenericTypeDefinition)
        {
            throw new ArgumentNullException($"{genericInterfaceDefinition} is not a generic interface definition.");
        }

        if (type.IsInterface)
        {
            if (type.IsGenericType)
            {
                Type interfaceDefinition = type.GetGenericTypeDefinition();

                if (genericInterfaceDefinition == interfaceDefinition)
                {
                    implementingType = type;
                    return true;
                }
            }
        }

        foreach (Type i in type.GetInterfaces())
        {
            if (i.IsGenericType)
            {
                Type interfaceDefinition = i.GetGenericTypeDefinition();

                if (genericInterfaceDefinition == interfaceDefinition)
                {
                    implementingType = i;
                    return true;
                }
            }
        }

        implementingType = null;
        return false;
    }
}