namespace Usul.Patterns;

public abstract class Builder<T> : IBuilder<T>
{
    public abstract T Build();
}

