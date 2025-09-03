using System.Collections.Concurrent;

namespace DotNetClone;


internal class ContractResolver
{
    private ConcurrentDictionary<Type, ICloneContract> _contracts = new();
    private DeepCloneSettings _settings = null!;

    internal void Init(DeepCloneSettings settings)
    {
        _settings = settings;
    }
    
    internal ICloneContract ResolveContract(Type type)
    {
        if (!_contracts.TryGetValue(type, out var contract))
        {
            var factory = _settings.ContractFactories.FirstOrDefault(e => e.AppliesTo(type))
                ?? throw new InvalidOperationException($"No contract factory found for type {type.FullName}");

            contract = factory.CreateContract(type, _settings);
            _contracts.TryAdd(type, contract);
        }
        return contract;
    }

    internal ICloneContract<T> ResolveContract<T>()
        => ResolveContract(typeof(T)) is ICloneContract<T> contract
            ? contract
            : throw new InvalidOperationException($"Contract for type {typeof(T).Name} is not of type ICloneContract<{typeof(T).Name}>");
}