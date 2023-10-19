using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private Vector2 menuScroll;
        private Vector2 scroll;

        private Dictionary<ISFConfig, string> _pathByMenu = new Dictionary<ISFConfig, string>();

        [MenuItem("Window/SFramework/Repositories")]
        private static void OpenWindow()
        {
            var window = GetWindow<SFConfigsWindow>();
            window.minSize = new Vector2(300f, 300f);
            window.titleContent = new GUIContent("Repositories", EditorIcons.Eject.Raw);
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
                    var repoType = repository.Key.Type.Replace("SF", "").Replace("Repository", "");
                    tree.Add($"Repositories/{repoType}/{repository.Key.Name}", repository.Key);
                    _pathByMenu[repository.Key] = repository.Value;
                }
            }

            return tree;
        }

        protected override void DrawMenu()
        {
            menuScroll = GUILayout.BeginScrollView(menuScroll);
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

            var repoType = repository.Type.Replace("SF", "").Replace("Repository", "");
            SirenixEditorGUI.Title(repository.Name, repoType, TextAlignment.Center, true);
            repository.Name = SirenixEditorGUI.DynamicPrimitiveField(new GUIContent("Name"), repository.Name);

            scroll = GUILayout.BeginScrollView(scroll);
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
                var savePath = EditorUtility.SaveFilePanel("Save Repository", Path.GetDirectoryName(path), Path.GetFileName(path),
                    "json");
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