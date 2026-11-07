using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SFramework.Configs.Editor;
using SFramework.Configs.Runtime;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace SFramework.Configs.Odin.Editor
{
    public class SFConfigsWindow : OdinMenuEditorWindow
    {
        private Vector2 _menuScroll;
        private Vector2 _scroll;

        private readonly Dictionary<ISFConfig, string> _pathByMenu = new(ConfigReferenceComparer.Instance);

        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        [MenuItem("Window/SFramework/Configs")]
        public static void OpenWindow()
        {
            if (EditorWindow.HasOpenInstances<SFConfigsWindow>())
            {
                EditorWindow.FocusWindowIfItsOpen<SFConfigsWindow>();
                return;
            }

            SFConfigsEditorUtility.RefreshConfigs();
            var window = GetWindow<SFConfigsWindow>();
            window.minSize = new Vector2(300f, 300f);
            window.titleContent = new GUIContent("Configs", EditorIcons.Eject.Raw);
            window.Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            _pathByMenu.Clear();

            var tree = new OdinMenuTree(false)
            {
                Config =
                {
                    DrawSearchToolbar = true
                },
                DefaultMenuStyle = OdinMenuStyle.TreeViewStyle
            };


            foreach (var type in GetInheritedClasses(typeof(ISFConfig)))
            {
                var repositories = SFConfigsEditorUtility.FindConfigsWithPaths(type);

                foreach (var repository in repositories)
                {
                    var repoType = repository.Key.Type.Replace("SF", "").Replace("GlobalConfig", "").Replace("Config", "");
                    repoType = Regex.Replace(repoType, @"\s+", "");
                    var withSpaces = Regex.Replace(repoType, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1");

                    if (repository.Key is ISFNodesConfig nodesConfig)
                    {
                        tree.Add($"Node Configs/{withSpaces}/{nodesConfig.Id}", repository.Key);
                    }
                    else
                    {
                        tree.Add($"Global Configs/{withSpaces}", repository.Key);
                    }

                    _pathByMenu[repository.Key] = repository.Value;
                }
            }

            tree.SortMenuItemsByName();

            return tree;
        }

        protected override void DrawMenu()
        {
            _menuScroll = GUILayout.BeginScrollView(_menuScroll);
            {
                base.DrawMenu();
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(1);

            if (GUILayout.Button("Reload", GUILayoutOptions.Height(20)))
            {
                Reload();
            }

            GUILayout.Space(2);
        }

        private void Reload()
        {
            SFConfigsEditorUtility.RefreshConfigs();
            ForceMenuTreeRebuild();
        }

        protected override void DrawEditor(int index)
        {
            var selection = MenuTree.Selection;
            var repository = selection.SelectedValue as ISFConfig;

            if (repository == null) return;

            var repoType = repository.Type.Replace("SF", "").Replace("Config", "").Replace("Global", "");
            
            if (repository is ISFNodesConfig nodesConfig)
            {
                var timestamp = SFConfigsEditorUtility.FromUnixTime(repository.Version);
                SirenixEditorGUI.Title(nodesConfig.Id, $"Version: {timestamp.ToString(CultureInfo.InvariantCulture)}", TextAlignment.Center, true);
            }
            
            if (repository is ISFGlobalConfig)
            {
                var timestamp = SFConfigsEditorUtility.FromUnixTime(repository.Version);
                SirenixEditorGUI.Title(repoType, $"Version: {timestamp.ToString(CultureInfo.InvariantCulture)}", TextAlignment.Center, true);
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            {
                base.DrawEditor(index);
            }

            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save")) SaveFromWindow(repository, WriteMode.Save);
                if (repository is not ISFNodesConfig) return;
                if (GUILayout.Button("Save As")) SaveFromWindow(repository, WriteMode.SaveAs);
                if (GUILayout.Button("New")) SaveFromWindow(repository, WriteMode.New);
            }
        }

        private void SaveFromWindow(ISFConfig repository, WriteMode mode)
        {
            try
            {
                if (!_pathByMenu.TryGetValue(repository, out var path))
                    throw new InvalidOperationException("The source file is unavailable. Reload the config list.");

                if (mode != WriteMode.Save)
                {
                    ResolveConfigPath(path, allowAbsolute: false);
                    var suggestion = ResolveConfigPath(AssetDatabase.GenerateUniqueAssetPath(path), allowAbsolute: false);
                    var title = mode == WriteMode.New ? "New Config" : "Save Config As";
                    path = EditorUtility.SaveFilePanel(title, Path.GetDirectoryName(suggestion),
                        Path.GetFileNameWithoutExtension(suggestion), "json");
                    if (string.IsNullOrEmpty(path)) return;
                }

                WriteConfig(repository, path, mode);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Could not save config", exception.Message, "OK");
                return;
            }

            AssetDatabase.Refresh();
            Reload();
        }

        private static void WriteConfig(ISFConfig source, string path, WriteMode mode)
        {
            var inPlace = mode == WriteMode.Save;
            var destination = ResolveConfigPath(path, allowAbsolute: !inPlace);
            if (inPlace && !File.Exists(destination))
                throw new FileNotFoundException("The source config was removed. Reload the config list.", destination);
            if (!inPlace && File.Exists(destination))
                throw new IOException($"A file already exists at '{destination}'. Choose a new name.");

            var config = mode == WriteMode.New ? (ISFConfig)Activator.CreateInstance(source.GetType()) : source;
            if (mode == WriteMode.New) config.Type = config.GetType().Name;

            var nodes = config as ISFNodesConfig;
            var previousId = nodes?.Id;
            var id = inPlace ? previousId : Path.GetFileNameWithoutExtension(destination);
            if (nodes != null) ValidateNodeId(config.GetType(), id, destination);

            var previousVersion = config.Version;
            var written = false;
            try
            {
                if (nodes != null && !inPlace) nodes.Id = id;
                if (mode != WriteMode.New) config.Version = DateTime.UtcNow.ToUnixTime();
                WriteAtomically(destination, JsonConvert.SerializeObject(config, Formatting.Indented), inPlace);
                written = true;
            }
            finally
            {
                if (mode == WriteMode.SaveAs && nodes != null) nodes.Id = previousId;
                if (mode == WriteMode.SaveAs || (inPlace && !written)) config.Version = previousVersion;
            }
        }

        private static void ValidateNodeId(Type configType, string id, string destination)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("A node config needs a root ID.");

            foreach (var assetPath in SFConfigsEditorUtility.FindConfigsWithPaths(configType).Values)
            {
                var path = ResolveConfigPath(assetPath, allowAbsolute: false);
                if (string.Equals(path, destination, PathComparison) || !File.Exists(path)) continue;

                // Cached objects may have unsaved edits. Compare against the actual saved root IDs.
                if (JsonConvert.DeserializeObject(File.ReadAllText(path), configType) is ISFNodesConfig saved &&
                    string.Equals(saved.Id, id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"The {configType.Name} ID '{id}' is already used by '{assetPath}'.");
                }
            }
        }

        private static string ResolveConfigPath(string path, bool allowAbsolute)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a config path under Assets.");

            var assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            path = path.Replace(Path.DirectorySeparatorChar, '/');
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
                path = Path.Combine(Path.GetDirectoryName(assets), path);
            else if (!allowAbsolute || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("Choose a config path under Assets.");

            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(assets + Path.DirectorySeparatorChar, PathComparison) ||
                Path.GetFileName(fullPath).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a .json file under the project's Assets folder.");

            // A junction or symlink inside Assets can redirect an otherwise valid path.
            for (var entry = fullPath; entry.Length > assets.Length; entry = Path.GetDirectoryName(entry))
            {
                if ((File.Exists(entry) || Directory.Exists(entry)) &&
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException($"Linked config paths are unavailable: '{entry}'.");
            }

            return fullPath;
        }

        private static void WriteAtomically(string destination, string contents, bool replace)
        {
            var temporary = Path.Combine(Path.GetDirectoryName(destination), $".{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, contents);
                if (replace) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private enum WriteMode
        {
            Save,
            SaveAs,
            New
        }

        private sealed class ConfigReferenceComparer : IEqualityComparer<ISFConfig>
        {
            public static readonly ConfigReferenceComparer Instance = new();

            public bool Equals(ISFConfig left, ISFConfig right) => ReferenceEquals(left, right);
            public int GetHashCode(ISFConfig config) => RuntimeHelpers.GetHashCode(config);
        }

        private Type[] GetInheritedClasses(Type type)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && type.IsAssignableFrom(t))
                .ToArray();
        }
    }
}
