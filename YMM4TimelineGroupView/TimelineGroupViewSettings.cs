using System;
using System.ComponentModel;
using System.Windows.Media;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;

namespace YMM4TimelineGroupView
{
    public enum GroupDisplayStyle
    {
        [Description("塗りつぶし＋枠線")]
        FillAndBorder = 0,

        [Description("枠線のみ")]
        BorderOnly = 1,

        [Description("塗りつぶしのみ")]
        FillOnly = 2
    }

    public enum GroupColorMode
    {
        [Description("カスタム単色")]
        CustomSingleColor = 0,

        [Description("グループごとに自動色分け")]
        AutoPerGroup = 1
    }

    public class TimelineGroupViewSettings : SettingsBase<TimelineGroupViewSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Other;

        public override string Name => "タイムライングループ表示";

        public override bool HasSettingView => true;

        [JsonIgnore]
        public override object? SettingView => new GroupViewSettingsView();

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Set(ref _isEnabled, value, nameof(IsEnabled));
        }

        private GroupDisplayStyle _displayStyle = GroupDisplayStyle.FillAndBorder;
        public GroupDisplayStyle DisplayStyle
        {
            get => _displayStyle;
            set => Set(ref _displayStyle, value, nameof(DisplayStyle));
        }

        private GroupColorMode _colorMode = GroupColorMode.CustomSingleColor;
        public GroupColorMode ColorMode
        {
            get => _colorMode;
            set => Set(ref _colorMode, value, nameof(ColorMode));
        }

        private Color _baseColor = Color.FromArgb(255, 0, 200, 115); // エメラルドグリーン
        public Color BaseColor
        {
            get => _baseColor;
            set => Set(ref _baseColor, value, nameof(BaseColor));
        }

        private double _fillOpacity = 0.25;
        public double FillOpacity
        {
            get => _fillOpacity;
            set => Set(ref _fillOpacity, Math.Clamp(value, 0.0, 1.0), nameof(FillOpacity));
        }

        private double _borderOpacity = 0.85;
        public double BorderOpacity
        {
            get => _borderOpacity;
            set => Set(ref _borderOpacity, Math.Clamp(value, 0.0, 1.0), nameof(BorderOpacity));
        }

        private double _borderThickness = 2.0;
        public double BorderThickness
        {
            get => _borderThickness;
            set => Set(ref _borderThickness, Math.Clamp(value, 1.0, 10.0), nameof(BorderThickness));
        }

        private double _cornerRadius = 4.0;
        public double CornerRadius
        {
            get => _cornerRadius;
            set => Set(ref _cornerRadius, Math.Clamp(value, 0.0, 20.0), nameof(CornerRadius));
        }

        private bool _showLabel = true;
        public bool ShowLabel
        {
            get => _showLabel;
            set => Set(ref _showLabel, value, nameof(ShowLabel));
        }

        private double _labelOpacity = 0.5; // 半透明ラベルデフォルト
        public double LabelOpacity
        {
            get => _labelOpacity;
            set => Set(ref _labelOpacity, Math.Clamp(value, 0.0, 1.0), nameof(LabelOpacity));
        }

        private bool _isLabelAbove = true; // 上部に浮かせる配置
        public bool IsLabelAbove
        {
            get => _isLabelAbove;
            set => Set(ref _isLabelAbove, value, nameof(IsLabelAbove));
        }

        private double _labelFontSize = 11.0;
        public double LabelFontSize
        {
            get => _labelFontSize;
            set => Set(ref _labelFontSize, Math.Clamp(value, 8.0, 24.0), nameof(LabelFontSize));
        }

        public override void Initialize()
        {
        }
    }
}
