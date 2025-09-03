namespace DotNetClone;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class CloneIgnoreAttribute : Attribute
{
    public CloneIgnoreAttribute() { }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class OnClonedAttribute: Attribute
{
    public OnClonedAttribute() { }
}