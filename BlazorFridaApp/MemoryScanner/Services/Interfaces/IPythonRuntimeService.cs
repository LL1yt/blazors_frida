namespace BlazorFridaApp.MemoryScanner.Services.Interfaces
{
    public interface IPythonRuntimeService
    {
        void EnsureInitialized();
        T ExecuteWithGIL<T>(Func<T> action);
        void ExecuteWithGIL(Action action);
        void ReleaseGIL();
    }
} 