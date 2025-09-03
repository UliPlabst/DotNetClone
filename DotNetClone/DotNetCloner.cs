using System.Collections.Immutable;
using System.Reflection;

namespace DotNetClone;

public class DeepCloneContext
{
    private Dictionary<object, object> ReferenceMap = [];

    public void AddReference(object original, object clone)
    {
        ReferenceMap.Add(original, clone);
    }

    public T? ResolveReference<T>(T original) where T : notnull
    {
        if (ReferenceMap.TryGetValue(original, out var clone))
            return (T)clone;
        return default;
    }
    
    public bool TryResolveReference<T>(T original, out T? clone)
    {
        if (ReferenceMap.TryGetValue(original!, out var resolvedClone))
        {
            clone = (T)resolvedClone;
            return true;
        }
        clone = default;
        return false;
    }

    public bool IsReferenced(object original)
        => ReferenceMap.ContainsKey(original);
}

public class DeepCloneSettingsBuilder
{
    public Func<MemberInfo, bool> ShouldCloneMember { get; set; }
        = e => e switch
        {
            PropertyInfo prop => prop.GetMethod!.IsPublic,
            FieldInfo field => false,
            _ => throw new NotSupportedException($"Unsupported member type: {e.GetType().Name}")
        };
        
    public Func<FieldInfo, bool> ShouldCloneField { get; set; }
        = e => false;
        
    public Func<Type, bool> ShouldUseAssignment { get; set; }
        = e => e.IsValueType || e == typeof(string);
        
    public Func<Type, ConstructorInfo> ResolveConstructor { get; set; }
        = e => e.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"No parameterless constructor found for type {e.FullName}");
            
    public List<ICloneContractFactory> ContractFactories { get; set; } = [];
    
    public DeepCloneSettingsBuilder AddContractFactory(ICloneContractFactory factory)
    {
        ContractFactories.Add(factory);
        return this;
    }
    
    public DeepCloneSettings Build()
    {
        ContractFactories.Add(new DefaultCloneContractFactory());
        var res = new DeepCloneSettings()
        {
            ShouldCloneMember = ShouldCloneMember,
            IsPrimitive = ShouldUseAssignment,
            ResolveConstructor = ResolveConstructor,
            ContractResolver = new(),
            ContractFactories = ContractFactories.ToImmutableArray()
        };
        res.ContractResolver.Init(res);
        return res;
    }
}



public class DeepCloneSettings
{
    public required Func<MemberInfo, bool> ShouldCloneMember { get; init; }
    public required Func<Type, bool> IsPrimitive { get; init; }
    public required Func<Type, ConstructorInfo> ResolveConstructor { get; init; }
    public required ImmutableArray<ICloneContractFactory> ContractFactories { get; init; } 

    internal ContractResolver ContractResolver { get; init; } = null!;
    internal DeepCloneSettings() { }
}

public static class DotNetCloner
{
    public static DeepCloneSettings DefaultSettings { get; set; } = new DeepCloneSettingsBuilder().Build();
        
    public static T DeepClone<T>(T source, DeepCloneSettings? settings = null)
    {
        settings ??= DefaultSettings;
        var context = new DeepCloneContext();
        return DeepCloneInternal(source, settings, context);
    }
    
    internal static T DeepCloneInternal<T>(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        if(source == null)
            return default!;
        if (context.TryResolveReference(source, out var existingClone))
            return existingClone!;
        var contract = settings.ContractResolver.ResolveContract(typeof(T));
        return contract switch
        {
            ICloneContract<T> cloneContract => cloneContract.DeepClone(source, settings, context),
            ICloneContract genericContract => (T)genericContract.DeepClone(source, settings, context)
        };
    }
    
    public static T ShallowClone<T>(T source, DeepCloneSettings? settings = null)
    {
        settings ??= DefaultSettings;
        var context = new DeepCloneContext();
        return ShallowCloneInternal(source, settings, context);
    }
    
    internal static T ShallowCloneInternal<T>(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        if(source == null)
            return default!;
        if (context.TryResolveReference(source, out var existingClone))
            return existingClone!;
        var contract = settings.ContractResolver.ResolveContract(typeof(T));
        return contract switch
        {
            ICloneContract<T> cloneContract => cloneContract.ShallowClone(source, settings, context),
            ICloneContract genericContract => (T)genericContract.ShallowClone(source, settings, context)
        };
    }
}