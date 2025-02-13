using Xunit;

namespace BlazorFridaApp.Tests.UI;

public class UITestConstants
{
    public const string BasicTests = "Basic UI Tests";
    public const string IntegrationTests = "UI Integration Tests";
    public const string PerformanceTests = "UI Performance Tests";
    public const string RegressionTests = "UI Regression Tests";
}

[CollectionDefinition(UITestConstants.BasicTests)]
public class BasicUITestCollection : ICollectionFixture<UITestFixture> { }

[CollectionDefinition(UITestConstants.IntegrationTests, DisableParallelization = true)]
public class IntegrationUITestCollection : ICollectionFixture<UITestFixture> { }

[CollectionDefinition(UITestConstants.PerformanceTests, DisableParallelization = true)]
public class PerformanceUITestCollection : ICollectionFixture<UITestFixture> { }

[CollectionDefinition(UITestConstants.RegressionTests)]
public class RegressionUITestCollection : ICollectionFixture<UITestFixture> { }