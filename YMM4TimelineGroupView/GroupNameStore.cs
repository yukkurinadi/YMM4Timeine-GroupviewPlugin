using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Newtonsoft.Json;
using YukkuriMovieMaker.Settings;

namespace YMM4TimelineGroupView
{
    /// <summary>
    /// グループ名をサイドカーJSON（プロジェクトと同フォルダ）に保存・読み込みするクラス。
    /// キー: GroupID (int) → 値: グループ名 (string)
    /// プロジェクトが未保存のときはメモリ内のみ保持する。
    /// </summary>
    public class GroupNameStore
    {
        private static GroupNameStore? _instance;
        public static GroupNameStore Instance => _instance ??= new GroupNameStore();

        // GroupID → 表示名
        private readonly Dictionary<int, string> _names = new();

        private string? _currentSidecarPath;
        private FileSystemWatcher? _watcher;

        public event EventHandler? NamesChanged;

        private GroupNameStore()
        {
            // FileSystemWatcher でカレントディレクトリの .ymmp 保存を検知
            SetupWatcher();
            // 初期ロード試行
            TryDetectAndLoad();
        }

        private void SetupWatcher()
        {
            try
            {
                // .ymmp が保存されるディレクトリを監視（最近使ったファイルから初回ディレクトリを特定）
                string watchDir = Environment.CurrentDirectory;
                if (!Directory.Exists(watchDir)) return;

                _watcher = new FileSystemWatcher(watchDir, "*.ymmp")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _watcher.Changed += OnYmmpFileChanged;
                _watcher.Created += OnYmmpFileChanged;
                _watcher.Renamed += (s, e) =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(() => TryDetectAndLoad());
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GroupNameStore] Watcher setup failed: {ex.Message}");
            }
        }

        private void OnYmmpFileChanged(object sender, FileSystemEventArgs e)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                UpdateSidecarPath(e.FullPath);
                TryLoad();
            });
        }

        /// <summary>プロジェクトファイルパスを MainWindow タイトルから取得する（フォールバック手段）</summary>
        private void TryDetectAndLoad()
        {
            try
            {
                // MainWindow タイトルからプロジェクトパスを推定
                string? title = Application.Current?.MainWindow?.Title;
                if (!string.IsNullOrEmpty(title))
                {
                    // タイトルに含まれるファイルパスを探す（"YMM4 - C:\...\proj.ymmp" 形式）
                    var parts = title.Split(new[] { " - ", " — " }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        string trimmed = part.Trim().TrimStart('*').Trim();
                        if (trimmed.EndsWith(".ymmp", StringComparison.OrdinalIgnoreCase) && File.Exists(trimmed))
                        {
                            UpdateSidecarPath(trimmed);
                            TryLoad();
                            return;
                        }
                    }
                }

                // カレントディレクトリの最新 .ymmp を検索
                string[] ymmps = Directory.GetFiles(Environment.CurrentDirectory, "*.ymmp");
                if (ymmps.Length > 0)
                {
                    string latest = ymmps.OrderByDescending(File.GetLastWriteTime).First();
                    UpdateSidecarPath(latest);
                    TryLoad();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GroupNameStore] TryDetectAndLoad failed: {ex.Message}");
            }
        }

        /// <summary>外部からプロジェクトファイルパスを通知する（Harmony パッチなどから呼ぶ用途）</summary>
        public void NotifyProjectPath(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return;
            UpdateSidecarPath(projectPath);
            TryLoad();
        }

        private void UpdateSidecarPath(string projectPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(projectPath) ?? "";
                string stem = Path.GetFileNameWithoutExtension(projectPath);
                string newPath = Path.Combine(dir, stem + ".groupnames.json");
                if (newPath == _currentSidecarPath) return;

                // プロジェクト切り替えならメモリをクリア
                _names.Clear();
                _currentSidecarPath = newPath;

                // watcher のディレクトリも更新
                if (_watcher != null && Directory.Exists(dir))
                {
                    _watcher.Path = dir;
                }
            }
            catch { }
        }

        private string GetEffectiveSidecarPath()
        {
            if (!string.IsNullOrEmpty(_currentSidecarPath)) return _currentSidecarPath;
            // 未保存プロジェクト時は %AppData%\YMM4GroupPlugin 以下に一時保存
            string tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YMM4GroupViewPlugin");
            Directory.CreateDirectory(tempDir);
            return Path.Combine(tempDir, "UnsavedProject.groupnames.json");
        }

        public string? GetName(int groupId) =>
            _names.TryGetValue(groupId, out var n) ? n : null;

        public void SetName(int groupId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                _names.Remove(groupId);
            else
                _names[groupId] = name.Trim();
            TrySave();
            NamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveGroup(int groupId)
        {
            if (_names.Remove(groupId))
            {
                TrySave();
                NamesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void TrySave()
        {
            try
            {
                string path = GetEffectiveSidecarPath();
                string json = JsonConvert.SerializeObject(_names, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GroupNameStore] Save failed: {ex.Message}");
            }
        }

        private void TryLoad()
        {
            try
            {
                string path = GetEffectiveSidecarPath();
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path);
                var loaded = JsonConvert.DeserializeObject<Dictionary<int, string>>(json);
                if (loaded != null)
                {
                    foreach (var kv in loaded)
                        _names[kv.Key] = kv.Value;
                }
                NamesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GroupNameStore] Load failed: {ex.Message}");
            }
        }
    }
}
