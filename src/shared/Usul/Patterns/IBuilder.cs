namespace Usul.Patterns;

internal interface IBuilder<out T>
{
    T Build();
}

