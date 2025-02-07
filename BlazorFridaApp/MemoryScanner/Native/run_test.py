import asyncio
import sys
from test_metrics import TestMetricsCollection


async def run_test():
    test = TestMetricsCollection()
    await test.asyncSetUp()
    try:
        await test.test_value_freezer()
        print("Test passed!")
    except Exception as e:
        print(f"Test failed: {str(e)}", file=sys.stderr)
        raise
    finally:
        await test.asyncTearDown()


if __name__ == "__main__":
    asyncio.run(run_test())
