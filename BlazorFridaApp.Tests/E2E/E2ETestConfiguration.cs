namespace BlazorFridaApp.Tests.E2E;

public class E2ETestConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost";
    public bool UseInMemoryDatabase { get; set; } = true;
    public bool MockExternalServices { get; set; } = true;
    public string TestEnvironment { get; set; } = "Test";

    public static E2ETestConfiguration Default => new();
}
