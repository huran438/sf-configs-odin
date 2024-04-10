using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private readonly Dictionary<ISFConfig, string> _pathByMenu = new();

        [MenuItem("Window/SFramework/Configs")]
        private static void OpenWindow()
        {
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
                var repositories = SFConfigsEditorExtensions.FindRepositoriesWithPaths(type);

                foreach (var repository in repositories)
                {
                    var repoType = repository.Key.Type.Replace("SF", "").Replace("Config", "");
                    var withSpaces = Regex.Replace(repoType, "((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", " $1");
                    tree.Add($"Configs/{withSpaces}/{repository.Key.Id}", repository.Key);
                    _pathByMenu[repository.Key] = repository.Value;
                }
            }

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
                ForceMenuTreeRebuild();
            }

            GUILayout.Space(2);
        }

        protected override void DrawEditor(int index)
        {
            var selection = MenuTree.Selection;
            var repository = selection.SelectedValue as ISFConfig;

            if (repository == null) return;

            var repoType = repository.Type.Replace("SF", "").Replace("Config", "");
            SirenixEditorGUI.Title(repository.Id, repoType, TextAlignment.Center, true);
            repository.Id = SirenixEditorGUI.DynamicPrimitiveField(new GUIContent("Name"), repository.Id);

            _scroll = GUILayout.BeginScrollView(_scroll);
            {
                base.DrawEditor(index);
            }

            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(1);

            if (GUILayout.Button("Save"))
            {
                var result = JsonConvert.SerializeObject(repository, Formatting.Indented);
                var path = Application.dataPath + _pathByMenu[repository].Replace("Assets", "");
                var savePath = EditorUtility.SaveFilePanel("Save Repository", Path.GetDirectoryName(path),
                    Path.GetFileName(path), "json");
                if (!string.IsNullOrEmpty(savePath))
                {
                    File.WriteAllText(savePath, result);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }

        private Type[] GetInheritedClasses(Type MyType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && MyType.IsAssignableFrom(t))
                .ToArray();
        }
    }
}