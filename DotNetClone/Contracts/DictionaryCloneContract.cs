namespace DotNetClone.Contracts;

public class ObjectDictionaryCloneContract<T, TKey, TValue>(
    bool isKeyPrimitive,
    bool isValuePrimitive
) : ICloneContract<T> where T: IDictionary<TKey, TValue>, new()
{
    public Type Type => typeof(T);

    public T Clone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        var clone = new T();
        context.AddReference(source, clone);
        foreach (var (srckey, srcvalue) in source)
        {
            var key = isKeyPrimitive
                ? srckey
                : DotNetCloner.DeepCloneInternal(srckey, settings, context);
            var value = isValuePrimitive
                ? srcvalue
                : DotNetCloner.DeepCloneInternal(srcvalue, settings, context);
            clone.Add(key, value);
        }
        return clone;
    }
}
