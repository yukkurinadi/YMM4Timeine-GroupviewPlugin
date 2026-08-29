using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using HarmonyLib;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Views;

namespace YMM4TimelineGroupView
{
    public class TimelineGroupViewPluginMain : IPlugin
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        public string Name => "タイムライングループ表示";

        public PluginDetailsAttribute? Details => new PluginDetailsAttribute
        {
            AuthorName = "YMM4 Community"
        };

        public TimelineGroupViewPluginMain()
        {
            EnsureInitialized();
        }

        [ModuleInitializer]
        public static void Initialize()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                try
                {
                    var harmony = new Harmony("com.ymm4.plugin.timelinegroupview");
                    ApplyPatches(harmony);
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TimelineGroupView Harmony Patch Failed: {ex}");
                }
            }
        }

        private static void ApplyPatches(Harmony harmony)
        {
            // TimelineView.InitializeComponent 後に自動で Adorner をアタッチ
            var targetMethod = AccessTools.Method(typeof(TimelineView), "InitializeComponent");
            if (targetMethod != null)
            {
                var postfix = new HarmonyMethod(typeof(TimelineViewPatch), nameof(TimelineViewPatch.InitializeComponent_Postfix));
                harmony.Patch(targetMethod, postfix: postfix);
            }
        }
    }

    public static class TimelineViewPatch
    {
        public static void InitializeComponent_Postfix(TimelineView __instance)
        {
            try
            {
                __instance.Loaded += (s, e) =>
                {
                    AttachAdorner(__instance);
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeComponent_Postfix: {ex}");
            }
        }

        private static void AttachAdorner(TimelineView timelineView)
        {
            try
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(timelineView);
                if (adornerLayer != null)
                {
                    var existingAdorners = adornerLayer.GetAdorners(timelineView);
                    if (existingAdorners != null)
                    {
                        foreach (var a in existingAdorners)
                        {
                            if (a is TimelineGroupAdorner) return; // 既に登録済み
                        }
                    }

                    var adorner = new TimelineGroupAdorner(timelineView);
                    adornerLayer.Add(adorner);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to attach TimelineGroupAdorner: {ex}");
            }
        }
    }
}
