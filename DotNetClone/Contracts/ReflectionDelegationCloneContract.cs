namespace DotNetClone;

public class ReflectionDelegationCloneContract<T> : ICloneContract<T> where T : class
{
    public Type Type => typeof(T);

    public T Clone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        if (source == null)
            return null!;
        var type = source.GetType();
        var contract = settings.ContractResolver.ResolveContract(type);
        return (T)contract.Clone(source, settings, context);
    }
}