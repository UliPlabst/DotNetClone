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

    private Func<T, DeepCloneSettings, DeepCloneContext, T>? _cloneFunc;
    private DeepCloneSettings _settings;

    public DefaultObjectCloneContract(DeepCloneSettings settings)
    {
        _settings = settings;
        CreateContract();
    }

    private void CreateContract()
    {
        var constructor = _settings.ResolveConstructor.Invoke(Type);
        //use system.linq.expressions to create a new and a member init expression

        var sourceParameter = Expression.Parameter(Type, "source");
        var settingsParameter = Expression.Parameter(typeof(DeepCloneSettings), "settings");
        var contextParameter = Expression.Parameter(typeof(DeepCloneContext), "context");

        var blockExpressions = new List<Expression>();
        var newExpression = Expression.New(constructor);
        var bindings = new List<MemberBinding>();

        var membersToClone = new List<MemberInfo>();
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var prop in Type.GetProperties(bindingFlags)
            .Where(e => e.CanRead && e.CanWrite && _settings.ShouldCloneMember(e))
        )
        {
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
            .Where(e => _settings.ShouldCloneMember.Invoke(e))
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
        _cloneFunc = lambda.Compile();
    }

    public T Clone(T source, DeepCloneSettings settings, DeepCloneContext context)
        => _cloneFunc!.Invoke(source, settings, context);
}