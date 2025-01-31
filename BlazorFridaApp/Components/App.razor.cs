using BlazorFridaApp.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorFridaApp.Components;

public partial class App : ComponentBase
{
    [Inject]
    public new AssetsService Assets { get; set; } = default!;
}