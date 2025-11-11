#if MODULES_PROFILER
using System;
using System.IO;
using System.Linq;
using System.Text;
using ModulesFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModulesFrameworkUnity.Debug.Diagnostics
{
    [Serializable]
    public class ProfilerTab
    {
        private VisualElement _root;
        private ScrollView _table;
        private ToolbarSearchField _search;
        private ToolbarMenu _sortMenu;
        private Label _info;

        private enum SortBy
        {
            AvgMs,
            LastMs,
            Calls,
            LastAlloc,
            Name
        }

        private SortBy _sort = SortBy.AvgMs;
        private bool _desc = true;
        private string _filter = string.Empty;

        private double _lastRefreshTime;

        public void Draw(VisualElement parent)
        {
            var ss = Resources.Load<StyleSheet>("ProfilerTabUSS");
            if (ss != null)
                parent.styleSheets.Add(ss);

            // корень вкладки
            _root = new VisualElement();
            _root.AddToClassList("modules--profiler-tab");
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);

            // === Toolbar ===
            var toolbar = new Toolbar();
            toolbar.AddToClassList("modules--profiler-tab--toolbar");

            _search = new ToolbarSearchField();
            _search.AddToClassList("modules--profiler-tab--search");
            _search.RegisterValueChangedCallback(ev =>
            {
                _filter = ev.newValue ?? string.Empty;
                Refresh();
            });
            toolbar.Add(_search);

            _sortMenu = new ToolbarMenu { text = "Sort: Avg ms ▼" };
            _sortMenu.AddToClassList("modules--profiler-tab--sort");
            _sortMenu.menu.AppendAction("Avg ms", _ => SetSort(SortBy.AvgMs));
            _sortMenu.menu.AppendAction("Last ms", _ => SetSort(SortBy.LastMs));
            _sortMenu.menu.AppendAction("Calls", _ => SetSort(SortBy.Calls));
            _sortMenu.menu.AppendAction("Last alloc", _ => SetSort(SortBy.LastAlloc));
            _sortMenu.menu.AppendAction("Name", _ => SetSort(SortBy.Name));
            toolbar.Add(_sortMenu);

            toolbar.Add(new ToolbarSpacer());

            var resetBtn = new ToolbarButton(() =>
                {
                    CoreProfiler.ResetAll();
                    Refresh();
                })
                { text = "Reset" };
            toolbar.Add(resetBtn);

            var exportBtn = new ToolbarButton(ExportCsv) { text = "Export CSV" };
            toolbar.Add(exportBtn);

            _root.Add(toolbar);

            // === Заголовки таблицы ===
            var header = new VisualElement();
            header.AddToClassList("modules--profiler-tab--header");
            header.Add(Header("System", "modules--profiler-tab--cell--name"));
            header.Add(Header("Calls", "modules--profiler-tab--cell--calls"));
            header.Add(Header("Avg ms", "modules--profiler-tab--cell--avg"));
            header.Add(Header("Last ms", "modules--profiler-tab--cell--last"));
            header.Add(Header("Min ticks", "modules--profiler-tab--cell--min"));
            header.Add(Header("Max ticks", "modules--profiler-tab--cell--max"));
            header.Add(Header("Alloc (KB, last)", "modules--profiler-tab--cell--alloc"));
            _root.Add(header);

            // === Таблица ===
            _table = new ScrollView();
            _table.AddToClassList("modules--profiler-tab--table");
            _root.Add(_table);

            // === Инфо ===
            _info = new Label("CoreProfiler snapshot. Updates every 1s.");
            _info.AddToClassList("modules--profiler-tab--info");
            _root.Add(_info);
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            _lastRefreshTime = EditorApplication.timeSinceStartup;
            Refresh();
            DebugEventBus.Update -= Tick;
            DebugEventBus.Update += Tick;
        }

        public void Hide()
        {
            DebugEventBus.Update -= Tick;
            _root.style.display = DisplayStyle.None;
        }

        /// <summary>Обновляем не чаще раза в секунду, чтобы не лагало UI</summary>
        public void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastRefreshTime < 1.0)
                return;

            _lastRefreshTime = now;
            Refresh();
        }

        public void Refresh()
        {
            if (_root == null || _root.style.display == DisplayStyle.None)
                return;

            var data = CoreProfiler.GetSnapshot();
            if (!string.IsNullOrEmpty(_filter))
                data = data.Where(s => s.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            Func<SystemStatsSnapshot, IComparable> key = _sort switch
            {
                SortBy.AvgMs => s => s.AvgMs,
                SortBy.LastMs => s => s.LastTicks * CoreProfiler.TicksToMs,
                SortBy.Calls => s => s.Calls,
                SortBy.LastAlloc => s => s.LastAllocBytes,
                SortBy.Name => s => s.Name,
                _ => s => s.AvgMs
            };

            data = (_desc ? data.OrderByDescending(key) : data.OrderBy(key)).ToList();

            _table.Clear();

            foreach (var s in data)
            {
                var lastMs = s.LastTicks * CoreProfiler.TicksToMs;
                var row = new VisualElement();
                row.AddToClassList("modules--profiler-tab--row");

                row.Add(Cell(s.Name, "modules--profiler-tab--cell--name"));
                row.Add(Cell(s.Calls.ToString(), "modules--profiler-tab--cell--calls"));
                row.Add(Cell(s.AvgMs.ToString("0.000"), "modules--profiler-tab--cell--avg"));
                row.Add(Cell(lastMs.ToString("0.000"), "modules--profiler-tab--cell--last"));
                row.Add(Cell(s.MinTicks.ToString(), "modules--profiler-tab--cell--min"));
                row.Add(Cell(s.MaxTicks.ToString(), "modules--profiler-tab--cell--max"));
                row.Add(Cell((s.LastAllocBytes / 1024.0).ToString("0.0"), "modules--profiler-tab--cell--alloc"));

                _table.Add(row);
            }

            _info.text = $"{data.Count} systems · updated: {DateTime.Now:HH:mm:ss}";
        }

        // Helpers
        private static Label Header(string text, string className)
        {
            var l = new Label(text);
            l.AddToClassList(className);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static Label Cell(string text, string className)
        {
            var l = new Label(text);
            l.AddToClassList(className);
            return l;
        }

        private void SetSort(SortBy s)
        {
            if (_sort == s)
                _desc = !_desc;
            else
            {
                _sort = s;
                _desc = true;
            }

            _sortMenu.text = $"Sort: {_sort} {(_desc ? "▼" : "▲")}";
            Refresh();
        }

        private void ExportCsv()
        {
            var path = EditorUtility.SaveFilePanel("Export ECS Profiler CSV", "", "ecs_profiler.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("name,calls,avg_ms,last_ms,min_ticks,max_ticks,last_alloc_kb,total_alloc_kb");
            foreach (var s in CoreProfiler.GetSnapshot())
            {
                var lastMs = s.LastTicks * CoreProfiler.TicksToMs;
                sb.AppendLine(
                    $"{Escape(s.Name)},{s.Calls},{s.AvgMs:0.###},{lastMs:0.###},{s.MinTicks},{s.MaxTicks},{s.LastAllocBytes / 1024.0:0.#},{s.TotalAllocBytes / 1024.0:0.#}");
            }

            File.WriteAllText(path, sb.ToString());
            EditorUtility.RevealInFinder(path);

            static string Escape(string v) => v.Contains(',') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
        }
    }
}
#endif