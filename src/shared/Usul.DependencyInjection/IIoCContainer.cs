namespace Usul.DependencyInjection;

public interface IIoCContainer : IDisposable, IAsyncDisposable
{
    public TInterface Resolve<TInterface>() where TInterface: notnull;
}
