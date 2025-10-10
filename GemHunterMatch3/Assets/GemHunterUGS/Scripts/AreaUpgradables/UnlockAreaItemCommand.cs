using UnityEngine;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Command for unlocking an area item. Handles both local state updates
    /// and command batching for cloud validation.
    /// </summary>
    public class UnlockAreaItemCommand : Command
    {
        protected override string k_CommandKey => "UnlockAreaItemCommand";
        private readonly int m_AreaItemId;
        
        public UnlockAreaItemCommand(int areaItemId)
        {
            m_AreaItemId = areaItemId;
        }
        
        public override void Execute()
        {
            LogCommand();
        }
        
        private void LogCommand()
        {
            Logger.LogVerbose($"UnlockAreaItem Command executed for ID: {m_AreaItemId}");
            // Note: Actual local state changes are handled by AreaManager
            // This command is primarily for batching and cloud validation
        }
    }
}
