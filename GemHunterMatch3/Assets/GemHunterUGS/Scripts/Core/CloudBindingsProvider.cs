using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Acts as a centralized provider for the auto-generated Cloud Code bindings.
    /// </summary>
    public class CloudBindingsProvider
    {
        public GemHunterUGSCloudBindings GemHunterBindings { get; } = new(CloudCodeService.Instance);
    }
}
