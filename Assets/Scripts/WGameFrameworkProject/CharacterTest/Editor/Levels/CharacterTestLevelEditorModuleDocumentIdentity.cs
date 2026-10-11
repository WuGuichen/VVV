using System;
using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    // Identity and source generation belong to the loaded draft, not the active Scene resolver.
    internal sealed class CharacterTestLevelEditorModuleDocumentIdentity
    {
        internal string LoadedPath { get; private set; }
        private string _scenePath, _levelId, _sourceHash;

        internal bool IsFor(string path) => string.Equals(LoadedPath, path, StringComparison.OrdinalIgnoreCase);

        internal void RequireCanLoad(string path, bool hasDocument, bool dirty, string module)
        {
            if (hasDocument && !IsFor(path) && dirty)
                throw new InvalidOperationException(module + "草稿仍属于 " + LoadedPath
                    + "；当前文档为 " + path + "。未保存草稿保留，请先处理原文档，未自动覆盖或丢弃。");
        }

        internal void Bind(string path, MxLevelAuthoringDocument envelope, string source)
        {
            LoadedPath = path; _scenePath = envelope.ScenePath; _levelId = envelope.LevelId;
            _sourceHash = source == null ? null : CharacterTestLevelCatalog.Hash(source);
        }

        internal void RequireSave(string path, string activeScene, MxLevelAuthoringDocument envelope,
            string source, string module)
        {
            if (!IsFor(path) || !string.Equals(_scenePath, envelope.ScenePath, StringComparison.Ordinal)
                || !string.Equals(_levelId, envelope.LevelId, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(envelope.ScenePath)
                    && !string.Equals(envelope.ScenePath, activeScene, StringComparison.Ordinal)))
                throw new InvalidOperationException(module + "文档身份不一致：已加载 " + LoadedPath
                    + " / " + _scenePath + "；当前 " + path + " / " + activeScene + "。草稿保留，未写入。");
            string actual = source == null ? null : CharacterTestLevelCatalog.Hash(source);
            if (!string.Equals(_sourceHash, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(module + "源文件已被其他入口修改或移除：" + LoadedPath
                    + "。当前草稿保留，未覆盖新来源；请先处理冲突再显式重载。");
        }

        internal void AcknowledgeSource(string source) => _sourceHash = source == null ? null : CharacterTestLevelCatalog.Hash(source);

        internal JObject Capture() => new JObject { ["path"] = LoadedPath, ["scenePath"] = _scenePath,
            ["levelId"] = _levelId, ["sourceHash"] = _sourceHash };

        internal void Restore(JObject state, string path, MxLevelAuthoringDocument envelope, string source)
        {
            if (state == null) { Bind(path, envelope, source); return; } // Previous recovery records.
            if (!string.Equals((string)state["path"], path, StringComparison.OrdinalIgnoreCase)
                || (string)state["scenePath"] != envelope.ScenePath || (string)state["levelId"] != envelope.LevelId)
                throw new InvalidOperationException("模块恢复载体与当前关卡身份不一致；恢复记录保留，未覆盖草稿。");
            LoadedPath = path; _scenePath = envelope.ScenePath; _levelId = envelope.LevelId;
            _sourceHash = (string)state["sourceHash"];
        }
    }
}
