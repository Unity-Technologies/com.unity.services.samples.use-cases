using UnityEngine;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Command for upgrading an area item. Handles both local state updates
    /// and command batching for cloud validation.
    /// </summary>
    public class UpgradeAreaItemCommand : Command
    {
        protected override string k_CommandKey => "UpgradeAreaItemCommand";
        private readonly int m_AreaItemId;
        
        public UpgradeAreaItemCommand(int areaItemId)
        {
            m_AreaItemId = areaItemId;
        }
        
        public override void Execute()
        {
            LogCommand();
        }

        private void LogCommand()
        {
            Logger.LogVerbose($"UpgradeAreaItem Command executed for ID: {m_AreaItemId}");
            // Note: Actual local state changes are handled by AreaManager
            // This command is primarily for batching and cloud validation
        }
    }
}
