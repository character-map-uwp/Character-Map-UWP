namespace CharacterMap.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependencyPropertyAttribute<T> : Attribute
{
    public string Name { get; set; }
    public object Default { get; set; }
    public string Callback { get; set; }
    public Type Type => typeof(T);

    public DependencyPropertyAttribute() { }

    public DependencyPropertyAttribute(string name)
    {
        Name = name;
    }

    public DependencyPropertyAttribute(string name, object def)
    {
        Name = name;
        Default = def;
    }

    public DependencyPropertyAttribute(string name, object def, string callback)
    {
        Name = name;
        Default = def;
        Callback = callback;
    }
}

public class AttachedPropertyAttribute<T> : DependencyPropertyAttribute<T>
{
    public AttachedPropertyAttribute() { }

    public AttachedPropertyAttribute(string name)
    {
        Name = name;
    }

    public AttachedPropertyAttribute(string name, object def)
    {
        Name = name;
        Default = def;
    }
}

public class DependencyPropertyAttribute : DependencyPropertyAttribute<object>
{
    public DependencyPropertyAttribute() { }
    public DependencyPropertyAttribute(string name)
    {
        Name = name;
    }

    public DependencyPropertyAttribute(string name, object def)
    {
        Name = name;
        Default = def;
    }

    public DependencyPropertyAttribute(string name, object def, string callback)
    {
        Name = name;
        Default = def;
        Callback = callback;
    }
}
