using System;
using System.Linq;
using Newtonsoft.Json;
using SFramework.Repositories.Runtime;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using SFEditorExtensions = SFramework.Repositories.Editor.SFEditorExtensions;

namespace SFramework.Repositories.Odin.Editor
{
    public class SFRepositoriesWindow : OdinMenuEditorWindow
    {
        private Vector2 menuScroll;
        private Vector2 scroll;

        [MenuItem("Window/SFramework/Repositories")]
        private static void OpenWindow()
        {
            var window = GetWindow<SFRepositoriesWindow>();
            window.minSize = new Vector2(300f, 300f);
            window.titleContent = new GUIContent("Repositories", EditorIcons.Eject.Raw);
            window.Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(false)
            {
                Config =
                {
                    DrawSearchToolbar = true
                },
                DefaultMenuStyle = OdinMenuStyle.TreeViewStyle
            };


            foreach (var type in GetInheritedClasses(typeof(ISFRepository)))
            {
                var repositories = SFEditorExtensions.FindRepositories(type);

                foreach (var repository in repositories)
                {
                    var repoType = repository.Type.Replace("SF", "").Replace("Repository", "");
                    tree.Add($"Repositories/{repoType}/{repository._Name}", repository);
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

            if (GUILayout.Button("Reload", GUILayoutOptions.Height(30)))
            {
                ForceMenuTreeRebuild();
            }

            GUILayout.Space(2);
        }

        protected override void DrawEditor(int index)
        {
            var selection = MenuTree.Selection;
            var repository = selection.SelectedValue as ISFRepository;

            if (repository != null)
            {
                var repoType = repository.Type.Replace("SF", "").Replace("Repository", "");
                SirenixEditorGUI.Title(repository._Name, repoType, TextAlignment.Center, true);
            }

            scroll = GUILayout.BeginScrollView(scroll);
            {
                base.DrawEditor(index);
            }

            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            SirenixEditorGUI.HorizontalLineSeparator(1);

            if (repository != null)
            {
                if (GUILayout.Button("Export"))
                {
                    GUIUtility.systemCopyBuffer = JsonConvert.SerializeObject(repository, Formatting.Indented);
                    Debug.Log("<color=green>Copied to clipboard</color>");
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