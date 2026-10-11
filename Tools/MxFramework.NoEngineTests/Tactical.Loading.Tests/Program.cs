using System.Reflection;
using NUnit.Framework;

int passed = 0;
foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
foreach (var method in type.GetMethods())
{
    if (method.GetCustomAttribute<TestAttribute>() == null) continue;
    try
    {
        object instance = Activator.CreateInstance(type);
        object result = method.Invoke(instance, null);
        if (result is Task pending) await pending;
        passed++;
        Console.WriteLine("PASS " + method.Name);
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error));
        return 1;
    }
}
Console.WriteLine($"{passed} production loading/checkpoint tests passed.");
return passed == 21 ? 0 : 1;
