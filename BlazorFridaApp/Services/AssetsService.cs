using Microsoft.Extensions.FileProviders;

namespace BlazorFridaApp.Services;

public class AssetsService
{
    private readonly IWebHostEnvironment _environment;

    public AssetsService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string this[string path]
    {
        get
        {
            var manifestPath = Path.Combine(_environment.WebRootPath, path);
            return $"/{path}";
        }
    }
}