#if MODULES_PROFILER
using ModulesFramework.Diagnostics;
using UnityEngine;

namespace ModulesFrameworkUnity.Diagnostics
{
    public static class ProfilerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
#if MODULES_PROFILER
            // CombinedScope: внутри делает CoreProfiler.Measure + ProfilerMarker.Auto()
            ProfilerBridge.Factory = static name => new CombinedScope(name);
#endif
        }
    }
}
#endif