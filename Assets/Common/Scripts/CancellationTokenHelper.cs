using System;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Services.Samples
{
    // See Microsoft's CancellationTokenSource docs
    // (https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource?view=net-6.0)
    // for more information about its purpose and functioning.
    public class CancellationTokenHelper : IDisposable
    {
        CancellationTokenSource m_CancellationTokenSource;
        bool m_Disposed;

        public CancellationToken cancellationToken => m_CancellationTokenSource.Token;

        public CancellationTokenHelper()
        {
            m_CancellationTokenSource = new CancellationTokenSource();
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.ExitingPlayMode)
            {
                m_CancellationTokenSource?.Cancel();
            }
        }
#endif

        // IDisposable related implementation modeled after
        // example code at https://learn.microsoft.com/en-us/dotnet/api/system.idisposable?view=net-6.0
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool triggeredByUserCode)
        {
            if (m_Disposed)
            {
                return;
            }

            // If triggeredByUserCode equals true, dispose both managed and unmanaged resources.
            if (triggeredByUserCode)
            {
                // Dispose managed resources.
                m_CancellationTokenSource.Dispose();
                m_CancellationTokenSource = null;
            }

#if UNITY_EDITOR

            // Clean up unmanaged resources.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

            m_Disposed = true;
        }

        ~CancellationTokenHelper()
        {
            Dispose(false);
        }
    }
}
