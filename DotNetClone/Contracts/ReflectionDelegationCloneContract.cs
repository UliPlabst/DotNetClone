namespace DotNetClone;

public class ReflectionDelegationCloneContract<T> : ICloneContract<T> where T : class
{
    public Type Type => typeof(T);

    public T DeepClone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        if (source == null)
            return null!;
        var type = source.GetType();
        var contract = settings.ContractResolver.ResolveContract(type);
        return (T)contract.DeepClone(source, settings, context);
    }
    
    public T ShallowClone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        if (source == null)
            return null!;
        var type = source.GetType();
        var contract = settings.ContractResolver.ResolveContract(type);
        return (T)contract.ShallowClone(source, settings, context);
    }
}
