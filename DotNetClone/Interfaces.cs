namespace DotNetClone;

public interface ICloneContract
{
    Type Type { get; }
    object Clone(object source, DeepCloneSettings settings, DeepCloneContext context);
}

public interface ICloneContract<T> : ICloneContract
{
    T Clone(T source, DeepCloneSettings settings, DeepCloneContext context);
    object ICloneContract.Clone(object source, DeepCloneSettings settings, DeepCloneContext context)
        => Clone((T)source!, settings, context)!;
}