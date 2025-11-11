#if MODULES_PROFILER
using System.Collections.Generic;
using Unity.Profiling;

namespace ModulesFrameworkUnity.Diagnostics
{
    internal static class UnityMarkerCache
    {
        private static readonly object _lock = new();
        private static readonly Dictionary<string, ProfilerMarker> _map = new(256);

        public static ProfilerMarker Get(string name)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(name, out var m)) return m;
#if UNITY_2020_2_OR_NEWER
                m = new ProfilerMarker(ProfilerCategory.Scripts, $"ECS/{name}");
#else
                m = new ProfilerMarker($"ECS/{name}");
#endif
                _map[name] = m;
                return m;
            }
        }
    }
}
#endif