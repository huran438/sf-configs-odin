using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SFramework.Configs.Editor;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;

namespace SFramework.Configs.Odin.Editor
{
    public class SFConfigsWindow : OdinMenuEditorWindow
    {
        private Vector2 _menuScroll;
        private Vector2 _scroll;

        private readonly Dictionary<ISFConfig, string> _pathByMenu = new();

        [MenuItem("Window/SFramework/Configs")]
        public static void OpenWindow()
        {
            if (EditorWindow.HasOpenInstances<SFConfigsWindow>())
            {
                EditorWindow.FocusWindowIfItsOpen<SFConfigsWindow>();
                return;
            }

            SFConfigsEditorExtensions.RefreshConfigs();
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
                var repositories = SFConfigsEditorExtensions.FindConfigsWithPaths(type);

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
            SFConfigsEditorExtensions.RefreshConfigs();
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
                var timestamp = SFConfigsEditorExtensions.FromUnixTime(repository.Version);
                SirenixEditorGUI.Title(nodesConfig.Id, $"Version: {timestamp.ToString(CultureInfo.InvariantCulture)}", TextAlignment.Center, true);
            }
            
            if (repository is ISFGlobalConfig globalConfig)
            {
                var timestamp = SFConfigsEditorExtensions.FromUnixTime(repository.Version);
                SirenixEditorGUI.Title(repoType, $"Version: {timestamp.ToString(CultureInfo.InvariantCulture)}", TextAlignment.Center, true);
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            {
                base.DrawEditor(index);
            }

            GUILayout.EndScrollView();

            if (repository is ISFNodesConfig _)
            {
                GUILayout.FlexibleSpace();
                SirenixEditorGUI.HorizontalLineSeparator(1);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    var path = Application.dataPath + _pathByMenu[repository].Replace("Assets", "");
                    var savePath = EditorUtility.SaveFilePanel("Save Config", Path.GetDirectoryName(path), Path.GetFileName(path), "json");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        repository.Version = DateTime.UtcNow.ToUnixTime();
                        var result = JsonConvert.SerializeObject(repository, Formatting.Indented);
                        File.WriteAllText(savePath, result);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }

                    Reload();
                }

                if (GUILayout.Button("New"))
                {
                    var path = Application.dataPath + _pathByMenu[repository].Replace("Assets", "");
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    var savePath = EditorUtility.SaveFilePanel("New Config", Path.GetDirectoryName(path), Path.GetFileName(path), "json");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        var config = Activator.CreateInstance(repository.GetType()) as ISFNodesConfig;
                        if (config == null)
                        {
                            SFDebug.Log(LogType.Error, "Config is NULL");
                            return;
                        }

                        config.Type = repository.GetType().Name;
                        config.Id = Path.GetFileNameWithoutExtension(path);
                        var result = JsonConvert.SerializeObject(config, Formatting.Indented);
                        File.WriteAllText(savePath, result);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }

                    Reload();
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.FlexibleSpace();
                SirenixEditorGUI.HorizontalLineSeparator(1);

                if (GUILayout.Button("Save"))
                {
                    repository.Version = DateTime.UtcNow.ToUnixTime();
                    var result = JsonConvert.SerializeObject(repository, Formatting.Indented);
                    var path = Application.dataPath + _pathByMenu[repository].Replace("Assets", "");
                    var savePath = EditorUtility.SaveFilePanel("Save Config", Path.GetDirectoryName(path),
                        Path.GetFileName(path), "json");
                    if (!string.IsNullOrEmpty(savePath))
                    {
                        File.WriteAllText(savePath, result);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }

                    Reload();
                }
            }
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