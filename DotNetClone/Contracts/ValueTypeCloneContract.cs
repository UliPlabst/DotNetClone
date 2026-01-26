namespace DotNetClone.Contracts;

public class ValueTypeCloneContract<T> : ICloneContract<T> where T : struct
{
    public Type Type => typeof(T);
    public T DeepClone(T source, DeepCloneSettings settings, DeepCloneContext context) => source;
    public T ShallowClone(T source, DeepCloneSettings settings, DeepCloneContext context) => source;
}