using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YMM4TimelineGroupView
{
    public partial class GroupViewSettingsView : UserControl
    {
        private bool _isUpdatingUi = false;

        public GroupViewSettingsView()
        {
            InitializeComponent();
            DataContext = TimelineGroupViewSettings.Default;

            InitializeColorPalette();
            UpdateUiFromColor(TimelineGroupViewSettings.Default.BaseColor);

            TimelineGroupViewSettings.Default.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TimelineGroupViewSettings.BaseColor))
                {
                    UpdateUiFromColor(TimelineGroupViewSettings.Default.BaseColor);
                }
            };
        }

        private void InitializeColorPalette()
        {
            var paletteColors = new (string name, Color color)[]
            {
                ("エメラルドグリーン", Color.FromArgb(255, 0, 200, 115)),
                ("ライトグリーン", Color.FromArgb(255, 76, 217, 100)),
                ("ターコイズ / 浅葱色", Color.FromArgb(255, 0, 199, 190)),
                ("シアン / 水色", Color.FromArgb(255, 50, 170, 255)),
                ("スカイブルー", Color.FromArgb(255, 0, 122, 255)),
                ("ロイヤルブルー", Color.FromArgb(255, 88, 86, 214)),
                ("パープル / 紫", Color.FromArgb(255, 175, 82, 222)),
                ("マゼンタ / 濃ピンク", Color.FromArgb(255, 235, 45, 140)),
                ("ローズ / ピンク", Color.FromArgb(255, 255, 45, 85)),
                ("レッド / 赤", Color.FromArgb(255, 255, 59, 48)),
                ("コーラル / 朱色", Color.FromArgb(255, 255, 100, 60)),
                ("オレンジ / 橙", Color.FromArgb(255, 255, 149, 0)),
                ("ゴールド / 山吹色", Color.FromArgb(255, 255, 190, 0)),
                ("イエロー / 黄", Color.FromArgb(255, 255, 214, 10)),
                ("ライム / 黄緑", Color.FromArgb(255, 160, 230, 30)),
                ("シルバーグレー", Color.FromArgb(255, 180, 185, 195)),
                ("ダークスレート", Color.FromArgb(255, 70, 80, 95)),
                ("ホワイト", Color.FromArgb(255, 255, 255, 255))
            };

            foreach (var (name, color) in paletteColors)
            {
                var btn = new Button
                {
                    ToolTip = name,
                    Margin = new Thickness(3),
                    Padding = new Thickness(0),
                    Width = 34,
                    Height = 26,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Content = new Border
                    {
                        Width = 28,
                        Height = 20,
                        CornerRadius = new CornerRadius(3),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                        Background = new SolidColorBrush(color)
                    }
                };

                var targetColor = color;
                btn.Click += (s, e) =>
                {
                    TimelineGroupViewSettings.Default.BaseColor = targetColor;
                };

                ColorPalettePanel.Children.Add(btn);
            }
        }

        private void UpdateUiFromColor(Color color)
        {
            if (_isUpdatingUi) return;
            _isUpdatingUi = true;
            try
            {
                CurrentColorBox.Background = new SolidColorBrush(color);
                HexTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                RedSlider.Value = color.R;
                GreenSlider.Value = color.G;
                BlueSlider.Value = color.B;

                RedText.Text = color.R.ToString();
                GreenText.Text = color.G.ToString();
                BlueText.Text = color.B.ToString();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi) return;

            byte r = (byte)Math.Clamp(RedSlider.Value, 0, 255);
            byte g = (byte)Math.Clamp(GreenSlider.Value, 0, 255);
            byte b = (byte)Math.Clamp(BlueSlider.Value, 0, 255);

            RedText.Text = r.ToString();
            GreenText.Text = g.ToString();
            BlueText.Text = b.ToString();

            var newColor = Color.FromArgb(255, r, g, b);
            CurrentColorBox.Background = new SolidColorBrush(newColor);
            HexTextBox.Text = $"#{r:X2}{g:X2}{b:X2}";

            TimelineGroupViewSettings.Default.BaseColor = newColor;
        }

        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;

            string hex = HexTextBox.Text.Trim().TrimStart('#');
            if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint val))
            {
                byte r = (byte)((val >> 16) & 0xFF);
                byte g = (byte)((val >> 8) & 0xFF);
                byte b = (byte)(val & 0xFF);

                _isUpdatingUi = true;
                try
                {
                    RedSlider.Value = r;
                    GreenSlider.Value = g;
                    BlueSlider.Value = b;

                    RedText.Text = r.ToString();
                    GreenText.Text = g.ToString();
                    BlueText.Text = b.ToString();

                    var newColor = Color.FromArgb(255, r, g, b);
                    CurrentColorBox.Background = new SolidColorBrush(newColor);
                    TimelineGroupViewSettings.Default.BaseColor = newColor;
                }
                finally
                {
                    _isUpdatingUi = false;
                }
            }
        }
    }
}
