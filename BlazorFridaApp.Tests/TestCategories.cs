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
public sealed class TestCategoryAttribute : Attribute
{
    private readonly TraitAttribute _trait;

    public TestCategoryAttribute(string category)
    {
        _trait = new TraitAttribute("Category", category);
    }
}
