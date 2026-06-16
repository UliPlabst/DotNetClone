using System.Linq.Expressions;
using System.Reflection;

namespace DotNetClone.Contracts;

public class DefaultObjectCloneContract<T> : ICloneContract<T>
{
    public Type Type { get; init; } = typeof(T);
    static MethodInfo _cloneMethod = typeof(DotNetCloner).GetMethod(nameof(DotNetCloner.DeepCloneInternal), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DeepClone method not found in DotNetCloner");

    static MethodInfo _resolveReferenceMethod = typeof(DeepCloneContext).GetMethod(nameof(DeepCloneContext.ResolveReference), BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("ResolveReference method not found in DeepCloneContext");

    static MethodInfo _isReferencedMethod = typeof(DeepCloneContext).GetMethod(nameof(DeepCloneContext.IsReferenced), BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("IsReferenced method not found in DeepCloneContext");

    static MethodInfo _addReferenceMethod = typeof(DeepCloneContext).GetMethod(nameof(DeepCloneContext.AddReference), BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException("AddReference method not found in DeepCloneContext");

    private Func<T, DeepCloneSettings, DeepCloneContext, T>? _deepCloneFunc;
    private Func<T, DeepCloneSettings, DeepCloneContext, T>? _shallowCloneFunc;
    private DeepCloneSettings _settings;

    public DefaultObjectCloneContract(DeepCloneSettings settings)
    {
        _settings = settings;
        CreateDeepCloneFunc();
        CreateShallowCloneFunc();
    }

    private void CreateDeepCloneFunc()
    {
        var constructor = _settings.ResolveConstructor.Invoke(Type);

        var sourceParameter = Expression.Parameter(Type, "source");
        var settingsParameter = Expression.Parameter(typeof(DeepCloneSettings), "settings");
        var contextParameter = Expression.Parameter(typeof(DeepCloneContext), "context");
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var (constructorArguments, constructorPropertyNames) = CreateConstructorArguments(
            constructor,
            sourceParameter,
            settingsParameter,
            contextParameter,
            bindingFlags,
            deepClone: true
        );

        var blockExpressions = new List<Expression>();
        var newExpression = Expression.New(constructor, constructorArguments);
        var bindings = new List<MemberBinding>();

        var membersToClone = new List<MemberInfo>();

        foreach (var prop in Type.GetProperties(bindingFlags)
            .Where(e => e.CanRead 
                && e.CanWrite 
                && !e.GetIndexParameters().Any()
                && _settings.ShouldCloneMember(e))
        )
        {
            if (constructorPropertyNames.Contains(prop.Name))
                continue;
            if (prop.HasAttribute<CloneIgnoreAttribute>())
                continue;
            if (_settings.IsPrimitive.Invoke(prop.PropertyType))
            {
                var value = Expression.Property(sourceParameter, prop);
                var binding = Expression.Bind(prop, value);
                bindings.Add(binding);
            }
            else
            {
                membersToClone.Add(prop);
            }
        }

        foreach (var field in Type.GetFields(bindingFlags)
            .Where(e => _settings.ShouldCloneMember.Invoke(e) && !e.IsSpecialName)
        )
        {
            if (field.HasAttribute<CloneIgnoreAttribute>())
                continue;
            if (_settings.IsPrimitive.Invoke(field.FieldType))
            {
                var value = Expression.Field(sourceParameter, field);
                var binding = Expression.Bind(field, value);
                bindings.Add(binding);
            }
            else
            {
                membersToClone.Add(field);
            }
        }

        var memberInit = Expression.MemberInit(newExpression, bindings);
        var variable = Expression.Variable(Type, "clone");
        var cloneAssignment = Expression.Assign(variable, memberInit);

        blockExpressions.Add(cloneAssignment);
        blockExpressions.Add(
            Expression.Call(contextParameter, _addReferenceMethod, sourceParameter, variable)
        );
        foreach (var member in membersToClone)
        {
            var (value, target, memberType) = member switch
            {
                PropertyInfo prop => (
                    Expression.Property(sourceParameter, prop),
                    Expression.Property(variable, prop),
                    prop.PropertyType
                ),
                FieldInfo field => (
                    Expression.Field(sourceParameter, field),
                    Expression.Field(variable, field),
                    field.FieldType
                ),
                _ => throw new InvalidOperationException($"Unsupported member type: {member.GetType()}")
            };
            var cloneMethod = _cloneMethod.MakeGenericMethod(memberType);
            var cloneCall = Expression.Call(cloneMethod, value, settingsParameter, contextParameter);
            var propAssignment = Expression.Assign(target, cloneCall);

            blockExpressions.Add(propAssignment);
        }

        var methods = Type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var onCloned = methods.FirstOrDefault(m => m.HasAttribute<OnClonedAttribute>(false))
            ?? methods.FirstOrDefault(m => m.HasAttribute<OnClonedAttribute>(true));

        if (onCloned != null)
        {
            var parameters = onCloned.GetParameters();
            if (parameters.Length != 2
                || parameters[0].ParameterType != Type
                || parameters[1].ParameterType != typeof(DeepCloneSettings)
            )
            {
                throw new InvalidOperationException($"OnCloned method {Type.Name}{onCloned.Name} must have exactly 2 parameters: (T src, DeepCloneSettings settings)");
            }

            if (onCloned.ReturnType != typeof(void))
                throw new InvalidOperationException($"OnCloned method {Type.Name}.{onCloned.Name} must return void");

            var onClonedCall = Expression.Call(variable, onCloned, [sourceParameter, settingsParameter]);
            blockExpressions.Add(onClonedCall);
        }

        //Return statement
        blockExpressions.Add(variable);

        var lambda = Expression.Lambda<Func<T, DeepCloneSettings, DeepCloneContext, T>>(
            Expression.Block(
                [variable],
                blockExpressions
            ),
            sourceParameter,
            settingsParameter,
            contextParameter
        );
        _deepCloneFunc = lambda.Compile();
    }
    
    private void CreateShallowCloneFunc()
    {
        var constructor = _settings.ResolveConstructor.Invoke(Type);

        var sourceParameter = Expression.Parameter(Type, "source");
        var settingsParameter = Expression.Parameter(typeof(DeepCloneSettings), "settings");
        var contextParameter = Expression.Parameter(typeof(DeepCloneContext), "context");
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var (constructorArguments, constructorPropertyNames) = CreateConstructorArguments(
            constructor,
            sourceParameter,
            settingsParameter,
            contextParameter,
            bindingFlags,
            deepClone: false
        );

        var blockExpressions = new List<Expression>();
        var newExpression = Expression.New(constructor, constructorArguments);
        var bindings = new List<MemberBinding>();

        var membersToClone = new List<MemberInfo>();

        foreach (var prop in Type.GetProperties(bindingFlags)
            .Where(e => e.CanRead 
                && e.CanWrite 
                && !e.GetIndexParameters().Any()
                && _settings.ShouldCloneMember(e))
        )
        {
            if (constructorPropertyNames.Contains(prop.Name))
                continue;
            if (prop.HasAttribute<CloneIgnoreAttribute>())
                continue;
            var value = Expression.Property(sourceParameter, prop);
            var binding = Expression.Bind(prop, value);
            bindings.Add(binding);
        }

        foreach (var field in Type.GetFields(bindingFlags)
            .Where(e => _settings.ShouldCloneMember.Invoke(e) && !e.IsSpecialName)
        )
        {
            var value = Expression.Field(sourceParameter, field);
            var binding = Expression.Bind(field, value);
            bindings.Add(binding);
        }

        var memberInit = Expression.MemberInit(newExpression, bindings);
        var lambda = Expression.Lambda<Func<T, DeepCloneSettings, DeepCloneContext, T>>(
            memberInit,
            sourceParameter,
            settingsParameter,
            contextParameter
        );
        _shallowCloneFunc = lambda.Compile();
    }

    private (Expression[] Arguments, HashSet<string> PropertyNames) CreateConstructorArguments(
        ConstructorInfo constructor,
        ParameterExpression sourceParameter,
        ParameterExpression settingsParameter,
        ParameterExpression contextParameter,
        BindingFlags bindingFlags,
        bool deepClone
    )
    {
        var parameters = constructor.GetParameters();
        var constructorPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (parameters.Length == 0)
            return ([], constructorPropertyNames);

        var properties = Type.GetProperties(bindingFlags)
            .Where(e => e.CanRead && !e.GetIndexParameters().Any())
            .DistinctBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

        var arguments = new Expression[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.Name == null || !properties.TryGetValue(parameter.Name, out var property))
                throw new InvalidOperationException($"Constructor parameter {Type.Name}.{parameter.Name ?? $"#{i}"} must match a property name");

            var argument = Expression.Property(sourceParameter, property);
            Expression value = deepClone && !_settings.IsPrimitive.Invoke(property.PropertyType)
                ? Expression.Call(_cloneMethod.MakeGenericMethod(property.PropertyType), argument, settingsParameter, contextParameter)
                : argument;

            if (value.Type != parameter.ParameterType)
                value = Expression.Convert(value, parameter.ParameterType);

            arguments[i] = value;
            constructorPropertyNames.Add(property.Name);
        }

        return (arguments, constructorPropertyNames);
    }

    public T DeepClone(T source, DeepCloneSettings settings, DeepCloneContext context)
        => _deepCloneFunc!.Invoke(source, settings, context);
        
    public T ShallowClone(T source, DeepCloneSettings settings, DeepCloneContext context)
        => _shallowCloneFunc!.Invoke(source, settings, context);
}