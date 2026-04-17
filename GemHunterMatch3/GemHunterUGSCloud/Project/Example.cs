using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud;

public class Example
{
    [CloudCodeFunction("SayHello")]
    public string Hello(string name)
    {
        return $"Hello, {name}!";
    }
    
    [CloudCodeFunction("TestFunction")]
    public Task<string> TestFunction(IExecutionContext context)
    {
        return Task.FromResult("TestFunction called successfully");
    }
}