using Xunit;

namespace BlazorFridaApp.Tests.UI;

public class RetryFactAttribute : FactAttribute
{
    private readonly int _maxRetries;
    private readonly int _delayMilliseconds;

    public RetryFactAttribute(int maxRetries = 3, int delayMilliseconds = 1000)
    {
        _maxRetries = maxRetries;
        _delayMilliseconds = delayMilliseconds;
    }

    public override string Skip
    {
        get => base.Skip;
        set
        {
            if (_maxRetries <= 0)
            {
                base.Skip = "Maximum retries must be greater than 0";
                return;
            }
            base.Skip = value;
        }
    }
}

public class RetryTheoryAttribute : TheoryAttribute
{
    private readonly int _maxRetries;
    private readonly int _delayMilliseconds;

    public RetryTheoryAttribute(int maxRetries = 3, int delayMilliseconds = 1000)
    {
        _maxRetries = maxRetries;
        _delayMilliseconds = delayMilliseconds;
    }

    public override string Skip
    {
        get => base.Skip;
        set
        {
            if (_maxRetries <= 0)
            {
                base.Skip = "Maximum retries must be greater than 0";
                return;
            }
            base.Skip = value;
        }
    }
}