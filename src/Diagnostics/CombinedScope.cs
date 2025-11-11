#if MODULES_PROFILER
using System;
using ModulesFramework.Diagnostics;
using Unity.Profiling;

namespace ModulesFrameworkUnity.Diagnostics
{
    /// <summary>Комбинированный скоуп: Core + Unity Profiler.</summary>
    public readonly struct CombinedScope : IDisposable
    {
        private readonly IDisposable _core;
        private readonly ProfilerMarker.AutoScope _unity;

        public CombinedScope(string name)
        {
            _core = CoreProfiler.Measure(name);
            _unity = UnityMarkerCache.Get(name).Auto();
        }

        public void Dispose()
        {
            _unity.Dispose();
            _core.Dispose();
        }
    }
}
#endif