using System.Security.Principal;

namespace BlazorFridaApp.Services;

public interface IAdminCheckService
{
    bool IsAdmin { get; }
}

public class AdminCheckService : IAdminCheckService
{
    public bool IsAdmin { get; }

    public AdminCheckService()
    {
        #if WINDOWS
        IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        #else
        IsAdmin = false;
        #endif
    }
}