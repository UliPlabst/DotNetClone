namespace DotNetClone.Contracts;

public class CollectionCloneContract<T, TElement>() 
    : ICloneContract<T> where T: ICollection<TElement>, new()
{
    public Type Type => typeof(T);

    public T DeepClone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        var clone = new T();
        context.AddReference(source, clone);
        var isElementPrimitive = settings.IsPrimitive(typeof(TElement));
        foreach (var item in source)
        {
            var element = isElementPrimitive
                ? item
                : DotNetCloner.DeepCloneInternal(item, settings, context);
            clone.Add(element);
        }
        return clone;
    }
    
    public T ShallowClone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        var clone = new T();
        context.AddReference(source, clone);
        foreach (var item in source)
        {
            clone.Add(item);
        }
        return clone;
    }
}

