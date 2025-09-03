namespace DotNetClone.Contracts;

public class ArrayCloneContract<T>() : ICloneContract<T[]>
{
    public Type Type => typeof(T);

    public T[] Clone(T[] source, DeepCloneSettings settings, DeepCloneContext context)
    {
        var clone = new T[source.Length];
        context.AddReference(source, clone);
        var isPrimitive = settings.IsPrimitive(typeof(T));
        for (int i = 0; i < source.Length; i++)
        {
            clone[i] = isPrimitive
                ? source[i]
                : DotNetCloner.DeepCloneInternal(source[i], settings, context);
        }
        return clone;
    }
}
