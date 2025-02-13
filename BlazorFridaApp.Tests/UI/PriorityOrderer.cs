[assembly: Xunit.TestCaseOrderer("BlazorFridaApp.Tests.UI.PriorityOrderer", "BlazorFridaApp.Tests")]

namespace BlazorFridaApp.Tests.UI;

public class PriorityAttribute : Attribute
{
    public PriorityAttribute(int priority)
    {
        Priority = priority;
    }

    public int Priority { get; }
}

public class PriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(
        IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
    {
        var assemblyName = typeof(PriorityAttribute).AssemblyQualifiedName!;
        var sortedMethods = new SortedDictionary<int, List<TTestCase>>();
        
        foreach (var testCase in testCases)
        {
            int priority = 0;

            foreach (var attr in testCase.TestMethod.Method.GetCustomAttributes(typeof(PriorityAttribute).AssemblyQualifiedName))
            {
                priority = attr.GetNamedArgument<int>("Priority");
                break;
            }

            GetOrCreate(sortedMethods, priority).Add(testCase);
        }

        foreach (var list in sortedMethods.Keys.Select(priority => sortedMethods[priority]))
        {
            list.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.TestMethod.Method.Name, y.TestMethod.Method.Name));
            foreach (var testCase in list)
            {
                yield return testCase;
            }
        }
    }

    private static TValue GetOrCreate<TKey, TValue>(
        IDictionary<TKey, TValue> dictionary, TKey key)
        where TValue : new()
    {
        if (dictionary.TryGetValue(key, out var result)) return result;
        result = new TValue();
        dictionary[key] = result;
        return result;
    }
}