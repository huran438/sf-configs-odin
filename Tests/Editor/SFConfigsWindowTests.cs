using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SFramework.Configs.Editor;
using SFramework.Configs.Odin.Editor;
using SFramework.Configs.Runtime;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace SFramework.Configs.Odin.Editor.Tests
{
    public sealed class SFConfigsWindowTests
    {
        private Dictionary<Type, Dictionary<ISFConfig, string>> _cache;
        private Dictionary<Type, Dictionary<ISFConfig, string>> _savedCache;
        private SFConfigsWindow _window;
        private string _testFolder;

        [SetUp]
        public void SetUp()
        {
            _cache = (Dictionary<Type, Dictionary<ISFConfig, string>>)typeof(SFConfigsEditorUtility)
                .GetField("_configsByType", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            _savedCache = new Dictionary<Type, Dictionary<ISFConfig, string>>(_cache);
            _cache.Clear();
            _window = ScriptableObject.CreateInstance<SFConfigsWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) UnityEngine.Object.DestroyImmediate(_window);
            if (_testFolder != null)
            {
                Directory.Delete(_testFolder, true);
                File.Delete(_testFolder + ".meta");
                _testFolder = null;
            }
            if (_cache == null || _savedCache == null) return;
            _cache.Clear();
            foreach (var pair in _savedCache) _cache.Add(pair.Key, pair.Value);
        }

        [Test]
        public void BuildMenuTree_GroupsGlobalAndNodeConfigsAndRetainsTheirSourcePaths()
        {
            ISFConfig global = new GlobalConfig { Type = "SFGameBalanceGlobalConfig", Version = 1 };
            ISFConfig nodes = new NodesConfig { Type = "SFQuestConfig", Id = "chapter-one", Version = 2 };
            AddToCache(typeof(SFGlobalConfig), global, "Assets/Configs/balance.json");
            AddToCache(typeof(SFNodesConfig), nodes, "Assets/Configs/chapter-one.json");

            var tree = BuildTree();
            var populatedItems = tree.EnumerateTree().Where(item => item.Value is ISFConfig).ToArray();

            Assert.That(tree.Config.DrawSearchToolbar, Is.True);
            Assert.That(populatedItems, Has.Length.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "Global Configs", "Game Balance" },
                populatedItems.Single(item => ReferenceEquals(item.Value, global)).GetFullPath().Split('/').Select(part => part.Trim()).ToArray());
            CollectionAssert.AreEqual(new[] { "Node Configs", "Quest", "chapter-one" },
                populatedItems.Single(item => ReferenceEquals(item.Value, nodes)).GetFullPath().Split('/').Select(part => part.Trim()).ToArray());
            Assert.That(Paths()[global], Is.EqualTo("Assets/Configs/balance.json"));
            Assert.That(Paths()[nodes], Is.EqualTo("Assets/Configs/chapter-one.json"));
        }

        [Test]
        public void BuildMenuTree_SortsNodeIdsRegardlessOfRepositoryInsertionOrder()
        {
            AddToCache(typeof(SFNodesConfig), new NodesConfig { Type = "SFQuestConfig", Id = "zeta" }, "Assets/zeta.json");
            AddToCache(typeof(SFNodesConfig), new NodesConfig { Type = "SFQuestConfig", Id = "alpha" }, "Assets/alpha.json");

            var tree = BuildTree();

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" },
                tree.EnumerateTree().Where(item => item.Value is ISFNodesConfig).Select(item => item.Name).ToArray());
        }

        [Test]
        public void Rebuild_RemovesDeletedRepositoriesAndTheirSavePaths()
        {
            ISFConfig removed = new NodesConfig { Type = "SFQuestConfig", Id = "removed" };
            AddToCache(typeof(SFNodesConfig), removed, "Assets/removed.json");
            BuildTree();
            Assert.That(Paths().ContainsKey(removed), Is.True);
            _cache.Clear();
            ISFConfig replacement = new GlobalConfig { Type = "SFGameGlobalConfig" };
            AddToCache(typeof(SFGlobalConfig), replacement, "Assets/replacement.json");

            var rebuilt = BuildTree();

            Assert.That(Paths(), Has.Count.EqualTo(1));
            Assert.That(Paths().ContainsKey(removed), Is.False);
            Assert.That(Paths()[replacement], Is.EqualTo("Assets/replacement.json"));
            CollectionAssert.AreEqual(new[] { replacement },
                rebuilt.EnumerateTree().Select(item => item.Value).OfType<ISFConfig>().ToArray());
        }

        [Test]
        public void BuildMenuTree_WithEmptyConfigCache_HasNoSelectableConfigsOrSavePaths()
        {
            var tree = BuildTree();

            Assert.That(tree.EnumerateTree().Any(item => item.Value is ISFConfig), Is.False);
            Assert.That(Paths(), Is.Empty);
        }

        [Test]
        public void Save_PreservesAssetsInTheMiddleOfASourcePath()
        {
            var destination = CreateTestPath("balance.json");
            File.WriteAllText(destination, "{}");
            ISFConfig repository = new GlobalConfig { Type = "SFGameBalanceGlobalConfig", Version = 1 };

            WriteConfig(repository, "Assets/" + Path.GetFileName(_testFolder) + "/balance.json", "Save");

            var json = JObject.Parse(File.ReadAllText(destination));
            Assert.That((string)json["Type"], Is.EqualTo("SFGameBalanceGlobalConfig"));
            Assert.That((long)json["Version"], Is.GreaterThan(1));
        }

        [Test]
        public void Save_WhenSerializationFailsPreservesTheExistingFileAndVersion()
        {
            var destination = CreateTestPath("original.json");
            const string original = "{\"Type\":\"Original\",\"Version\":1}";
            File.WriteAllText(destination, original);
            ISFConfig source = new BrokenConfig { Type = "Original", Version = 1 };

            var error = Assert.Throws<TargetInvocationException>(() =>
                WriteConfig(source, "Assets/" + Path.GetFileName(_testFolder) + "/original.json", "Save"));

            Assert.That(error.InnerException, Is.InstanceOf<Newtonsoft.Json.JsonSerializationException>());
            Assert.That(File.ReadAllText(destination), Is.EqualTo(original));
            Assert.That(source.Version, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(_testFolder), Is.EquivalentTo(new[] { destination }));
        }

        [Test]
        public void SaveAs_UsesTheSelectedIdAndKeepsTheSourceObjectUnchanged()
        {
            ISFNodesConfig source = new NodesConfig { Type = nameof(NodesConfig), Id = "source", Version = 1 };
            var destination = CreateTestPath("selected.json");

            WriteConfig(source, destination, "SaveAs");

            var json = JObject.Parse(File.ReadAllText(destination));
            Assert.That((string)json["Id"], Is.EqualTo("selected"));
            Assert.That((long)json["Version"], Is.GreaterThan(1));
            Assert.That(source.Id, Is.EqualTo("source"));
            Assert.That(source.Version, Is.EqualTo(1));
        }

        [Test]
        public void New_UsesTheSelectedIdAndRefusesAnExistingFile()
        {
            ISFNodesConfig source = new NodesConfig { Type = nameof(NodesConfig), Id = "source", Version = 1 };
            var destination = CreateTestPath("selected.json");

            WriteConfig(source, destination, "New");

            var contents = File.ReadAllText(destination);
            var json = JObject.Parse(contents);
            Assert.That((string)json["Id"], Is.EqualTo("selected"));
            Assert.That((long)json["Version"], Is.EqualTo(0));
            Assert.That(Assert.Throws<TargetInvocationException>(() => WriteConfig(source, destination, "New"))
                .InnerException, Is.InstanceOf<IOException>());
            Assert.That(File.ReadAllText(destination), Is.EqualTo(contents));
        }

        [Test]
        public void SaveAs_RejectsAPersistedDuplicateEvenWhenItsCachedIdHasChanged()
        {
            ISFNodesConfig source = new NodesConfig { Type = nameof(NodesConfig), Id = "source" };
            var existingPath = CreateTestPath("existing.json");
            File.WriteAllText(existingPath, "{\"Type\":\"NodesConfig\",\"Id\":\"selected\"}");
            AddToCache(typeof(NodesConfig), new NodesConfig { Id = "edited" },
                "Assets/" + Path.GetFileName(_testFolder) + "/existing.json");
            var destination = CreateTestPath("selected.json");

            var error = Assert.Throws<TargetInvocationException>(() => WriteConfig(source, destination, "SaveAs"));

            Assert.That(error.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(error.InnerException.Message, Does.Contain("selected"));
            Assert.That(File.Exists(destination), Is.False);
        }

        [TestCase("Assets/../ProjectSettings/config.json")]
        [TestCase("Packages/sp.sframework.configs/config.json")]
        [TestCase("Assets/config.txt")]
        public void Save_RejectsInvalidSourcePaths(string sourcePath)
        {
            ISFConfig source = new GlobalConfig { Type = "SFGameBalanceGlobalConfig", Version = 1 };

            var error = Assert.Throws<TargetInvocationException>(() => WriteConfig(source, sourcePath, "Save"));

            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(source.Version, Is.EqualTo(1));
        }

        private string CreateTestPath(string fileName)
        {
            _testFolder ??= Path.Combine(Application.dataPath, "TestAssets" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
            return Path.Combine(_testFolder, fileName);
        }

        private static void WriteConfig(ISFConfig repository, string path, string action)
        {
            var method = typeof(SFConfigsWindow).GetMethod("WriteConfig", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new[] { repository, path, Enum.Parse(method.GetParameters()[2].ParameterType, action) });
        }

        private void AddToCache(Type key, ISFConfig config, string path)
        {
            if (!_cache.TryGetValue(key, out var repositories))
                _cache[key] = repositories = new Dictionary<ISFConfig, string>();
            repositories.Add(config, path);
        }

        private OdinMenuTree BuildTree() => (OdinMenuTree)typeof(SFConfigsWindow)
            .GetMethod("BuildMenuTree", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_window, null);

        private Dictionary<ISFConfig, string> Paths() => (Dictionary<ISFConfig, string>)typeof(SFConfigsWindow)
            .GetField("_pathByMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_window);

        // Struct fixtures are deliberately excluded from RefreshConfigs' concrete-class scan.
        // The existing abstract cache keys let the real window consume these repositories.
        private struct GlobalConfig : ISFGlobalConfig
        {
            public string Type { get; set; }
            public long Version { get; set; }
        }

        private struct BrokenConfig : ISFGlobalConfig
        {
            public string Type { get; set; }
            public long Version { get; set; }
            public string Broken => throw new InvalidOperationException("Serialization failed.");
        }

        private struct NodesConfig : ISFNodesConfig
        {
            public string Type { get; set; }
            public long Version { get; set; }
            public string Id { get; set; }
            public string FullId { get; set; }
            public ISFConfigNode Parent { get; set; }
            public ISFConfigNode[] Children => null;
            public void BuildTree() { }
        }
    }
}
