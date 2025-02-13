using Xunit.Sdk;

namespace BlazorFridaApp.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RetryAttribute : BeforeAfterTestAttribute
{
    private readonly int _maxRetries;
    private readonly int _delayMilliseconds;

    public RetryAttribute(int maxRetries = 3, int delayMilliseconds = 1000)
    {
        _maxRetries = maxRetries;
        _delayMilliseconds = delayMilliseconds;
    }

    public override void Before(System.Reflection.MethodInfo methodUnderTest)
    {
        // No setup needed
    }

    public override void After(System.Reflection.MethodInfo methodUnderTest)
    {
        var testException = TestContext.Current?.Exception;
        if (testException == null) return;

        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                Task.Delay(_delayMilliseconds * (i + 1)).Wait(); // Exponential backoff
                methodUnderTest.Invoke(TestContext.Current?.TestClass, null);
                TestContext.Current.Exception = null; // Clear the exception if retry succeeds
                return;
            }
            catch (Exception ex)
            {
                testException = ex;
            }
        }

        TestContext.Current.Exception = testException;
    }
}
