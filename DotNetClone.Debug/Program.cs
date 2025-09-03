using DotNetClone;
using Newtonsoft.Json.Linq;

namespace DotNetClone.Debug
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var src = new Test();
            src.Hello.World = 100;
            src.Property = "111";
            src.Hello2 = src.Hello;
            var r = DotNetCloner.DeepClone(src, new DeepCloneSettingsBuilder()
                .AddContractFactory(new JTokenCloneContractFactory())
                .Build()
            );
            Console.WriteLine(r.Hello == r.Hello2);
            Console.WriteLine(r.JO);
        }
    }
}

public class JTokenCloneContractFactory : ICloneContractFactory
{
public bool AppliesTo(Type type) => typeof(JToken).IsAssignableFrom(type);
public ICloneContract CreateContract(Type type, DeepCloneSettings settings)
{
    var contractType = typeof(JTokenCloneContract<>).MakeGenericType(type);
    return (ICloneContract)Activator.CreateInstance(contractType)!;
}

public class JTokenCloneContract<T> : ICloneContract<T> where T : JToken
{
    public Type Type => typeof(T);

    public T Clone(T source, DeepCloneSettings settings, DeepCloneContext context)
    {
        return (T)source.DeepClone();
    }
}
}
public class Test
{
    public string Property { get; set; } = "asd";
    public Hello Hello { get; set; } = new Hello();
    public Hello Hello2 { get; set; }
    public string[] Array { get; set; } = ["a", "b", "c"];
    public Hello Hello3 { get; set; } = null;
    public HashSet<string> Set { get; set; } = new HashSet<string>()
    {
        "1", "2", "3"
    };
    public ICollection<string> Strings = new List<string>()
    {
        "a", "b", "c"
    };
    public JObject JO { get; set; } = new JObject()
    {
        ["De"] = "123"
    };

    public IReadOnlyDictionary<string, string> Dict { get; set; } = new Dictionary<string, string>()
    {
        ["a"] = "1",
        ["b"] = "2"
    };


    [OnCloned]
    public void DD(Test src, DeepCloneSettings settings)
    {
        Console.WriteLine("asd");
    }
}

public class Hello
{
    public decimal World { get; set; } = 10.5m;
}