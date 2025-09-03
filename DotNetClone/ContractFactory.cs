using DotNetClone.Contracts;

namespace DotNetClone;

public interface ICloneContractFactory
{
    bool AppliesTo(Type type);
    ICloneContract CreateContract(Type type, DeepCloneSettings settings);
}

public class DefaultCloneContractFactory : ICloneContractFactory
{
    public bool AppliesTo(Type type) => true;
    
    public ICloneContract CreateContract(Type type, DeepCloneSettings settings)
    {
        if (type == typeof(object))
            return new ReflectionDelegationCloneContract<object>();
        if(type.IsArray)    
            return CreateArrayContract(type);
        if (type.IsInterface || type.IsAbstract)
            return CreateReflectionDelegationCloneContract(type);
        if (type.TryGetGenericTypeImplementation(typeof(IDictionary<,>), out var implementingType))
            return CreateDictionaryContract(type, implementingType, settings);
        if(type.TryGetGenericTypeImplementation(typeof(ICollection<>), out implementingType))
            return CreateCollectionContract(type, implementingType);
        return CreateDefaultObjectCloneContract(type, settings);
    }
    
    ICloneContract CreateCollectionContract(Type type, Type implementingType)
    {
        var elementType = implementingType.GetGenericArguments()[0];
        var collectionCloneContractType = typeof(CollectionCloneContract<,>)
            .MakeGenericType(type, elementType);
        return (ICloneContract)Activator.CreateInstance(collectionCloneContractType)!;
    }
    
    ICloneContract CreateArrayContract(Type type)
    {
        var arrayCloneContractType = typeof(ArrayCloneContract<>).MakeGenericType(type.GetElementType()!);
        return (ICloneContract)Activator.CreateInstance(arrayCloneContractType)!;
    }

    ICloneContract CreateDictionaryContract(Type type, Type implementingType, DeepCloneSettings settings)
    {
        var genericArgs = implementingType.GetGenericArguments();
        var keyType = genericArgs[0];
        var valueType = genericArgs[1];

        var dictionaryCloneContractType = typeof(ObjectDictionaryCloneContract<,,>)
            .MakeGenericType(type, keyType, valueType);

        return (ICloneContract)Activator.CreateInstance(dictionaryCloneContractType, [
            settings.IsPrimitive(keyType),
            settings.IsPrimitive(valueType)
        ])!;
    }
    
    ICloneContract CreateReflectionDelegationCloneContract(Type type)
    {
        var contractType = typeof(ReflectionDelegationCloneContract<>).MakeGenericType(type);
        return (ICloneContract)Activator.CreateInstance(contractType)!;
    }

    ICloneContract CreateDefaultObjectCloneContract(Type type, DeepCloneSettings settings)
    {
        var contractType = typeof(DefaultObjectCloneContract<>).MakeGenericType(type);
        return (ICloneContract)Activator.CreateInstance(contractType, [settings])!;
    }
}
