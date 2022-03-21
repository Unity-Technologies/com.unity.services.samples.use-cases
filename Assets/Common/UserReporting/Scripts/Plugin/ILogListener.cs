using UnityEngine;

namespace Assets.UserReporting.Scripts.Plugin
{
    public interface ILogListener
    {
        #region Methods

        void ReceiveLogMessage(string logString, string stackTrace, LogType logType);

        #endregion
    }
}