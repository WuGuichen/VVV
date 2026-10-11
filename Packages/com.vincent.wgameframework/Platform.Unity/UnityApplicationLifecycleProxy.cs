using System;
using UnityEngine;

namespace MxFramework.Platform.Unity
{
    /// <summary>
    /// A composition-owned Unity callback proxy that forwards facts to an explicitly bound reporter.
    /// </summary>
    /// <remarks>
    /// This component owns no policy, persistence, RuntimeHost, scene, UI, singleton, or hidden lifetime.
    /// Callback forwarding is NoAlloc after binding.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnityApplicationLifecycleProxy : MonoBehaviour
    {
        private IApplicationLifecycleReporter _reporter;

        /// <summary>Gets whether a composition root has bound a reporter.</summary>
        public bool IsBound => _reporter != null;

        /// <summary>Binds the reporter that receives Unity lifecycle facts.</summary>
        /// <remarks>LowFreqAlloc only for invalid-argument diagnostics.</remarks>
        public void Bind(IApplicationLifecycleReporter reporter)
        {
            if (reporter == null)
                throw new ArgumentNullException(nameof(reporter));

            _reporter = reporter;
        }

        /// <summary>Removes the current reporter without replacing its owner.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void Unbind()
        {
            _reporter = null;
        }

        internal void OnApplicationFocus(bool hasFocus)
        {
            _reporter?.ReportFocus(hasFocus);
        }

        internal void OnApplicationPause(bool pauseStatus)
        {
            _reporter?.ReportSuspend(pauseStatus);
        }

        internal void OnApplicationQuit()
        {
            _reporter?.ReportQuitting();
        }
    }
}
