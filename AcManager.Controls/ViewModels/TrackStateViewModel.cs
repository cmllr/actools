using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Controls.ViewModels {
    /// <summary>
    /// Full version with presets. Load-save-switch between presets-save as a preset, full
    /// package. Also, provides previews for presets!
    /// </summary>
    public class TrackStateViewModel : TrackStateViewModelBase, IUserPresetable, IPresetsPreviewProvider {
        private static TrackStateViewModel _instance;

        public static TrackStateViewModel Instance => _instance ?? (_instance = new TrackStateViewModel("qdtrackstate"));

        public TrackStateViewModel([Localizable(false)] string customKey = null) : base(customKey, false) {
            PresetableKey = customKey ?? PresetableCategory;
            Saveable.Initialize();
        }

        protected override void SaveLater() {
            base.SaveLater();
            Changed?.Invoke(this, new EventArgs());
        }

        #region Presetable
        bool IUserPresetable.CanBeSaved => true;

        public string PresetableKey { get; }

        string IUserPresetable.PresetableCategory => PresetableCategory;

        string IUserPresetable.DefaultPreset => "Green";

        public string ExportToPresetData() {
            return Saveable.ToSerializedString();
        }

        public event EventHandler Changed;

        public void ImportFromPresetData(string data) {
            Saveable.FromSerializedString(data);
        }

        object IPresetsPreviewProvider.GetPreview(string data) {
            return new UserControls.TrackStateDescription { DataContext = CreateFixed(data) };
        }
        #endregion
    }

    public class TrackStatesHelper : IDisposable {
        private readonly string _templates;
        private readonly FileSystemWatcher _watcher;

        private TrackStatesHelper() {
            _templates = Path.Combine(FileUtils.GetDocumentsCfgDirectory(), "templates");
            RegisterBuiltInPresets();

            Directory.CreateDirectory(_templates);
            _watcher = new FileSystemWatcher {
                Path = _templates,
                Filter = "tracks.ini",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += Handler;
            _watcher.Created += Handler;
            _watcher.Deleted += Handler;
            _watcher.Renamed += Handler;
        }

        private bool _inProgress;

        private async Task ReloadLater() {
            if (_inProgress) return;

            try {
                _inProgress = true;

                for (var i = 0; i < 10; i++) {
                    try {
                        await Task.Delay(300);
                        (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(RegisterBuiltInPresets);
                        break;
                    } catch (IOException) {}
                }
            } finally {
                _inProgress = false;
            }
        }

        private void Handler(object sender, FileSystemEventArgs e) {
            ReloadLater().Forget();
        }

        private static readonly Regex NameRegex = new Regex(@"(?<=\b[A-Z])[A-Z]+", RegexOptions.Compiled);

        [NotNull]
        private static string NameFromId([NotNull] string id) {
            return NameRegex.Replace(id, x => x.Value.ToLower(CultureInfo.CurrentUICulture));
        }

        private IEnumerable<Tuple<string, TrackStateViewModelBase>> GetBuiltInPresets() {
            var filename = Path.Combine(_templates, "tracks.ini");
            var ini = new IniFile(filename);
            foreach (var pair in ini) {
                yield return Tuple.Create(NameFromId(pair.Key), TrackStateViewModelBase.CreateBuiltIn(pair.Value));
            }
        }

        private void RegisterBuiltInPresets() {
            PresetsManager.Instance.ClearBuiltInPresets(TrackStateViewModelBase.PresetableCategory);
            foreach (var preset in GetBuiltInPresets()) {
                PresetsManager.Instance.RegisterBuiltInPreset(preset.Item2.ToBytes(), TrackStateViewModelBase.PresetableCategory, preset.Item1);
            }

            UserPresetsControl.RescanCategory(TrackStateViewModelBase.PresetableCategory, true);
        }

        public void Dispose() {
            _watcher.EnableRaisingEvents = false;
            _watcher?.Dispose();
        }

        public static TrackStatesHelper Instance { get; private set; }

        public static void Initialize() {
            Instance = new TrackStatesHelper();
        }
    }
}