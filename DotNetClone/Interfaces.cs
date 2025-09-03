namespace DotNetClone;

public interface ICloneContract
{
    Type Type { get; }
    object DeepClone(object source, DeepCloneSettings settings, DeepCloneContext context);
    object ShallowClone(object source, DeepCloneSettings settings, DeepCloneContext context);
}

public interface ICloneContract<T> : ICloneContract
{
    T DeepClone(T source, DeepCloneSettings settings, DeepCloneContext context);
    T ShallowClone(T source, DeepCloneSettings settings, DeepCloneContext context);
    object ICloneContract.DeepClone(object source, DeepCloneSettings settings, DeepCloneContext context)
        => DeepClone((T)source!, settings, context)!;
        
    object ICloneContract.ShallowClone(object source, DeepCloneSettings settings, DeepCloneContext context)
        => ShallowClone((T)source!, settings, context)!;
}