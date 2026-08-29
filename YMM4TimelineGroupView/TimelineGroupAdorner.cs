using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.ViewModels;
using YukkuriMovieMaker.Views;

namespace YMM4TimelineGroupView
{
    public class TimelineGroupAdorner : Adorner
    {
        private readonly TimelineView _timelineView;
        private TimelineViewModel? _timelineVm;
        private Timeline? _timeline;
        private Grid? _viewboxGrid;

        private readonly List<IItem> _hookedItems = new();
        private readonly DispatcherTimer _pollTimer;

        public TimelineGroupAdorner(TimelineView timelineView) : base(timelineView)
        {
            _timelineView = timelineView;
            IsHitTestVisible = false; // マウスイベントを受け取らない（完全透過）

            TimelineGroupViewSettings.Default.PropertyChanged += OnSettingsChanged;
            GroupNameStore.Instance.NamesChanged += (s, e) => InvalidateVisual();
            _timelineView.DataContextChanged += OnDataContextChanged;
            _timelineView.Loaded += (s, e) => HookTimeline();

            // 30ms ポーリングタイマー（ドラッグ移動中もリアルタイム追従）
            _pollTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _pollTimer.Tick += (s, e) => InvalidateVisual();
            _pollTimer.Start();

            HookTimeline();
        }

        // ── ヒットテストの制御（常に null を返して完全に背後に通過させる） ───────────
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return null!;
        }

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => HookTimeline();

        private void HookTimeline()
        {
            if (_timelineView.DataContext is TimelineViewModel tvm)
            {
                if (_timelineVm != tvm)
                {
                    _timelineVm = tvm;
                    _timelineVm.Viewport.Subscribe(_ => InvalidateVisual());
                    _timelineVm.TimelineZoom.Subscribe(_ => InvalidateVisual());

                    var timelineField = _timelineVm.GetType().GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (timelineField != null)
                    {
                        _timeline = timelineField.GetValue(_timelineVm) as Timeline;
                    }

                    if (_timeline != null)
                    {
                        _timeline.PropertyChanged += OnTimelinePropertyChanged;
                        HookItems();
                    }
                }
            }

            if (SettingsBase<YMMSettings>.Default != null)
                SettingsBase<YMMSettings>.Default.PropertyChanged += OnYmmSettingsChanged;

            InvalidateVisual();
        }

        private void HookItems()
        {
            UnhookItems();
            if (_timeline == null) return;
            foreach (var item in _timeline.Items)
            {
                item.PropertyChanged += OnItemPropertyChanged;
                _hookedItems.Add(item);
            }
        }

        private void UnhookItems()
        {
            foreach (var item in _hookedItems)
                item.PropertyChanged -= OnItemPropertyChanged;
            _hookedItems.Clear();
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Frame" or "Length" or "Layer" or "Group" or "IsHidden")
                InvalidateVisual();
        }

        private void OnTimelinePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "Items" or "CurrentFrame")
            {
                HookItems();
                InvalidateVisual();
            }
        }

        private void OnYmmSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "TimelineZoom" or "LayerHeight")
                InvalidateVisual();
        }

        private Grid? GetViewboxGrid()
        {
            if (_viewboxGrid != null) return _viewboxGrid;
            var field = typeof(TimelineView).GetField("viewboxGrid", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) _viewboxGrid = field.GetValue(_timelineView) as Grid;
            return _viewboxGrid;
        }

        // ── 描画 ─────────────────────────────────────────────────────
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var settings = TimelineGroupViewSettings.Default;
            if (!settings.IsEnabled) return;

            if (_timeline == null && _timelineVm != null)
            {
                var f = _timelineVm.GetType().GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { _timeline = f.GetValue(_timelineVm) as Timeline; HookItems(); }
            }

            if (_timeline == null || _timeline.Items.IsEmpty) return;

            var viewboxGrid = GetViewboxGrid();
            if (viewboxGrid == null || !viewboxGrid.IsVisible) return;

            Point origin;
            try { origin = viewboxGrid.TranslatePoint(new Point(0, 0), _timelineView); }
            catch { return; }

            double gridWidth = viewboxGrid.ActualWidth;
            double gridHeight = viewboxGrid.ActualHeight;
            if (gridWidth <= 0 || gridHeight <= 0) return;

            double zoom = SettingsBase<YMMSettings>.Default?.TimelineZoom ?? 100.0;
            double layerHeight = SettingsBase<YMMSettings>.Default?.LayerHeight ?? 28.0;
            Rect viewport = _timelineVm?.Viewport?.Value ?? new Rect(0, 0, gridWidth, gridHeight);

            var groups = _timeline.Items
                .Where(x => x.Group != 0)
                .GroupBy(x => x.Group)
                .Select(g => new
                {
                    GroupId = g.Key,
                    MinFrame = g.Min(x => x.Frame),
                    MaxEndFrame = g.Max(x => x.Frame + x.Length),
                    MinLayer = g.Min(x => x.Layer),
                    MaxLayer = g.Max(x => x.Layer)
                })
                .OrderBy(g => g.MinFrame)
                .ThenBy(g => g.MinLayer)
                .ToList();

            if (groups.Count == 0) return;

            dc.PushClip(new RectangleGeometry(new Rect(origin.X, origin.Y, gridWidth, gridHeight)));
            try
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    int displayIndex = i + 1;

                    double canvasX = (g.MinFrame * zoom / 100.0) - viewport.X;
                    double canvasY = (g.MinLayer * layerHeight) - viewport.Y;
                    double width = (g.MaxEndFrame - g.MinFrame) * zoom / 100.0;
                    double height = (g.MaxLayer - g.MinLayer + 1) * layerHeight;

                    double left = origin.X + canvasX;
                    double top = origin.Y + canvasY;

                    if (left + width < origin.X - 100 || left > origin.X + gridWidth + 100 ||
                        top + height < origin.Y - 100 || top > origin.Y + gridHeight + 100)
                        continue;

                    Color baseColor = settings.ColorMode == GroupColorMode.AutoPerGroup
                        ? GetAutoColor(displayIndex) : settings.BaseColor;

                    Brush? fillBrush = null;
                    Pen? borderPen = null;

                    if (settings.DisplayStyle is GroupDisplayStyle.FillAndBorder or GroupDisplayStyle.FillOnly)
                    {
                        fillBrush = new SolidColorBrush(Color.FromArgb(
                            (byte)(settings.FillOpacity * 255), baseColor.R, baseColor.G, baseColor.B));
                        fillBrush.Freeze();
                    }

                    if (settings.DisplayStyle is GroupDisplayStyle.FillAndBorder or GroupDisplayStyle.BorderOnly)
                    {
                        var bc = new SolidColorBrush(Color.FromArgb(
                            (byte)(settings.BorderOpacity * 255), baseColor.R, baseColor.G, baseColor.B));
                        borderPen = new Pen(bc, settings.BorderThickness);
                        borderPen.Freeze();
                    }

                    var rect = new Rect(left, top, Math.Max(2.0, width), Math.Max(2.0, height));
                    dc.DrawRoundedRectangle(fillBrush, borderPen, rect, settings.CornerRadius, settings.CornerRadius);

                    if (settings.ShowLabel)
                    {
                        DrawGroupLabel(dc, g.GroupId, displayIndex, left, top, width, baseColor, settings, origin.X, origin.Y);
                    }
                }
            }
            finally { dc.Pop(); }
        }

        private void DrawGroupLabel(DrawingContext dc, int groupId, int displayIndex,
            double x, double y, double width, Color baseColor, TimelineGroupViewSettings settings, double gridLeft, double gridTop)
        {
            string displayName = GroupNameStore.Instance.GetName(groupId)
                                 ?? $"グループ {displayIndex}";

            double labelOpacity = settings.LabelOpacity;

            var textBrush = new SolidColorBrush(Color.FromArgb((byte)(labelOpacity * 255), 255, 255, 255));
            textBrush.Freeze();

            var formattedText = new FormattedText(
                displayName,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI, Meiryo, sans-serif"),
                    FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                settings.LabelFontSize, textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double paddingX = 5.0, paddingY = 1.5;
            double bgWidth = formattedText.Width + paddingX * 2;
            double bgHeight = formattedText.Height + paddingY * 2;

            // ── ラベルの X 座標計算（固定表示オプションの反映） ──
            double labelX = x;
            if (settings.IsLabelSticky)
            {
                // グループの左端が見切れても画面左端（gridLeft）に追従し、
                // グループの右端を超えて押し出されないように制限
                double maxLabelX = x + width - bgWidth;
                labelX = Math.Max(x, Math.Min(gridLeft + 2.0, maxLabelX));
            }

            double labelTop = settings.IsLabelAbove && y - bgHeight >= gridTop
                ? y - bgHeight : y + 2.0;

            var labelBgRect = new Rect(labelX, labelTop, bgWidth, bgHeight);

            var bgBrush = new SolidColorBrush(Color.FromArgb((byte)(labelOpacity * 200), 20, 20, 20));
            bgBrush.Freeze();
            var bBrush = new SolidColorBrush(Color.FromArgb((byte)(labelOpacity * 255), baseColor.R, baseColor.G, baseColor.B));
            bBrush.Freeze();
            var bPen = new Pen(bBrush, 1.0); bPen.Freeze();

            dc.DrawRoundedRectangle(bgBrush, bPen, labelBgRect, 3.0, 3.0);
            dc.DrawText(formattedText, new Point(labelX + paddingX, labelTop + paddingY));
        }

        private static Color GetAutoColor(int id)
        {
            double hue = (id * 137.508) % 360.0;
            return HsvToRgb(hue, 0.75, 0.90);
        }

        private static Color HsvToRgb(double h, double s, double v)
        {
            int hi = (int)(Math.Floor(h / 60.0)) % 6;
            double f = h / 60.0 - Math.Floor(h / 60.0);
            byte V = (byte)(v * 255), p = (byte)(v * (1 - s) * 255),
                 q = (byte)(v * (1 - f * s) * 255), t = (byte)(v * (1 - (1 - f) * s) * 255);
            return hi switch
            {
                0 => Color.FromRgb(V, t, p),
                1 => Color.FromRgb(q, V, p),
                2 => Color.FromRgb(p, V, t),
                3 => Color.FromRgb(p, q, V),
                4 => Color.FromRgb(t, p, V),
                _ => Color.FromRgb(V, p, q),
            };
        }
    }
}