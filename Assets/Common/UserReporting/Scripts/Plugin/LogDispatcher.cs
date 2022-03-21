using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.UserReporting.Scripts.Plugin
{
    public static class LogDispatcher
    {
        #region Static Constructors

        static LogDispatcher()
        {
            LogDispatcher.listeners = new List<WeakReference>();
            Application.logMessageReceivedThreaded += (logString, stackTrace, logType) =>
            {
                lock (LogDispatcher.listeners)
                {
                    int i = 0;
                    while (i < LogDispatcher.listeners.Count)
                    {
                        WeakReference listener = LogDispatcher.listeners[i];
                        ILogListener logListener = listener.Target as ILogListener;
                        if (logListener != null)
                        {
                            logListener.ReceiveLogMessage(logString, stackTrace, logType);
                            i++;
                        }
                        else
                        {
                            LogDispatcher.listeners.RemoveAt(i);
                        }
                    }
                }
            };
        }

        #endregion

        #region Static Fields

        private static List<WeakReference> listeners;

        #endregion

        #region Static Methods

        public static void Register(ILogListener logListener)
        {
            lock (LogDispatcher.listeners)
            {
                LogDispatcher.listeners.Add(new WeakReference(logListener));
            }
        }

        #endregion
    }
}