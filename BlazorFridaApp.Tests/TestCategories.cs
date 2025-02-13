using Xunit;

namespace BlazorFridaApp.Tests;

public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Component = "Component";
    public const string Integration = "Integration";
    public const string UI = "UI";
    public const string E2E = "E2E";
    public const string Smoke = "Smoke";
    public const string Performance = "Performance";
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class TestCategoryAttribute : Xunit.TraitAttribute
{
    public TestCategoryAttribute(string category) 
        : base("Category", category)
    {
    }
}
