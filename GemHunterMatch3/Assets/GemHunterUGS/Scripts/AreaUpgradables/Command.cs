using System;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Base class for area-related commands, providing common structure for command execution and identification.
    /// </summary>
    public abstract class Command
    {
        protected abstract string k_CommandKey { get; }
        
        // Timestamp for when command is created
        public DateTime Timestamp { get; private set; }

        protected Command()
        {
            Timestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Executes the command's local effects
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// Gets the command's identifier for cloud processing
        /// </summary>
        public string GetKey() => k_CommandKey;
        
        /// <summary>
        /// Gets the command's time-signed key for secure cloud validation.
        /// Includes the command type and a timestamp in ISO 8601 format.
        /// </summary>
        public string GetTimeSignedKey() => $"{k_CommandKey}|{Timestamp:o}";
    }
}
