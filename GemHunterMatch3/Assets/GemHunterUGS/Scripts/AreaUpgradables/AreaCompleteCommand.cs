using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Command for completing an area. Unlike other commands which are enqueued externally,
    /// this command adds itself to the batch system on execution.
    /// </summary>
    /// <remarks>
    /// This command marks the end of a command batch and triggers cloud validation.
    /// It must be the last command in any batch as no further upgrades or unlocks
    /// are allowed after area completion.
    /// </remarks>
    public class AreaCompleteCommand : Command
    {
        protected override string k_CommandKey => "AreaCompleteCommand";
        
        public override void Execute()
        {
            LogCommand();
        }

        private void LogCommand()
        {
            Logger.LogVerbose($"AreaCompleteCommand executed");
            // Note: Area completion state and rewards are handled by Cloud Code
            // This command primarily serves as a batch terminator in Cloud Code
        }
    }
}
