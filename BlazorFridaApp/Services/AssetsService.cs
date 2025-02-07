using Microsoft.Extensions.FileProviders;

namespace BlazorFridaApp.Services;

public class AssetsService
{
    private readonly IWebHostEnvironment _environment;

    public AssetsService(IWebHostEnvironment environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public string this[string path]
    {
        get
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            var manifestPath = Path.Combine(_environment.WebRootPath, path);
            return $"/{path}";
        }
    }
}