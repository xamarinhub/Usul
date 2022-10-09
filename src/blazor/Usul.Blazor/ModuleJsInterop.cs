using System.Reflection;
using Microsoft.JSInterop;

namespace Usul.Blazor
{
    public class ModuleJsInterop : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

        public static ModuleJsInterop FromContent(IJSRuntime jsRuntime, string path) =>
            FromContent(jsRuntime, path, Assembly.GetCallingAssembly());

        public static ModuleJsInterop FromContent(IJSRuntime jsRuntime, string path, Assembly assembly) =>
            FromContent(jsRuntime, path, assembly.GetName().Name!);

        public static ModuleJsInterop FromContent(IJSRuntime jsRuntime, string path, string assemblyName) =>
            new (jsRuntime, $"./_content/{assemblyName}/{path}");

        public ModuleJsInterop(IJSRuntime jsRuntime, string name) =>
            _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "name").AsTask());
        

        public async ValueTask<T> InvokeAsync<T>(string functionName, IEnumerable<object> parameters, CancellationToken cancellationToken = default)
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<T>(functionName, cancellationToken, parameters);
        }

        public async ValueTask InvokeVoideAsync(string functionName, IEnumerable<object> parameters, CancellationToken cancellationToken = default)
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync(functionName, cancellationToken, parameters);
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            if (_moduleTask.IsValueCreated)
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}