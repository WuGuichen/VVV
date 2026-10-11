using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Editor.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    // Only exact registered transforms are reverted on live objects. Their other components,
    // including water authoring components, retain all current values. Hierarchies are serialized
    // only to recreate an owned object that was deleted while editing this document.
    internal sealed class CharacterTestLevelEditorOwnedSceneSnapshot
    {
        [Serializable]
        internal sealed class ComponentState
        {
            public Component Original;
            public string TypeName, Json;
            public ulong Id;
        }

        [Serializable]
        internal sealed class Node
        {
            public Transform Original, Parent;
            public string Name, Tag, Prefab;
            public int Layer, Sibling;
            public bool Active;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public ulong Id, ParentId;
            public List<ComponentState> Components = new List<ComponentState>();
            public List<Node> Children = new List<Node>();
        }

        private readonly List<Node> _baseline;
        private readonly Dictionary<Transform, Object[]> _added = new Dictionary<Transform, Object[]>();
        private readonly UnityEngine.SceneManagement.Scene _scene;

        private CharacterTestLevelEditorOwnedSceneSnapshot(List<Transform> transforms)
        {
            _scene = transforms.Count == 0 ? EditorSceneManager.GetActiveScene() : transforms[0].gameObject.scene;
            _baseline = transforms.Select(Capture).ToList();
        }

        [Serializable]
        private sealed class ReloadComponent
        {
            public string TypeName, Json;
            public ulong Id;
        }

        [Serializable]
        private sealed class ReloadNode
        {
            public string Name, Tag, Prefab;
            public int Layer, Sibling;
            public bool Active;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public ulong Id, ParentId;
            public List<ReloadComponent> Components;
            public int[] Children;
        }

        [Serializable]
        private sealed class ReloadAdded
        {
            public ulong TransformObjectId;
            public ulong[] ObjectIds;
        }

        [Serializable]
        private sealed class ReloadState
        {
            public int Version = 2;
            public ulong SceneHandle;
            public string ScenePath;
            public List<ReloadNode> Nodes = new List<ReloadNode>();
            public int[] Roots;
            public List<ReloadAdded> Added;
        }

        internal string CaptureReloadState()
        {
            // Flat DTOs avoid JsonUtility's depth limit on recursively typed Node trees.
            // Native identities, rather than serialized managed wrappers, own rebinds.
            var state = new ReloadState { SceneHandle = _scene.handle.GetRawData(), ScenePath = _scene.path,
                Added = _added.Select(p => new ReloadAdded {
                    TransformObjectId = Id(p.Key == null ? null : p.Key.gameObject),
                    ObjectIds = p.Value.Select(Id).ToArray() }).ToList() };
            state.Roots = _baseline.Select(n => FlattenReloadNode(n, state.Nodes)).ToArray();
            return JsonUtility.ToJson(state);
        }

        private static int FlattenReloadNode(Node node, List<ReloadNode> nodes)
        {
            int index = nodes.Count;
            var value = new ReloadNode { Name = node.Name, Tag = node.Tag, Prefab = node.Prefab,
                Layer = node.Layer, Sibling = node.Sibling, Active = node.Active,
                Position = node.Position, Rotation = node.Rotation, Scale = node.Scale,
                Id = node.Id, ParentId = node.ParentId,
                Components = node.Components.Select(c => new ReloadComponent {
                    TypeName = c.TypeName, Json = c.Json, Id = c.Id }).ToList() };
            nodes.Add(value);
            value.Children = node.Children.Select(n => FlattenReloadNode(n, nodes)).ToArray();
            return index;
        }

        internal static CharacterTestLevelEditorOwnedSceneSnapshot RestoreReloadState(string json)
        {
            var state = JsonUtility.FromJson<ReloadState>(json);
            var scene = EditorSceneManager.GetActiveScene();
            if (state == null || state.Version != 2 || state.Nodes == null || state.Roots == null || state.Added == null
                || state.ScenePath != scene.path || state.SceneHandle != scene.handle.GetRawData())
                throw new InvalidOperationException("恢复范围基线不属于当前已加载 Scene；恢复记录保留。");
            var nodes = state.Nodes.Select(n => new Node { Name = n.Name, Tag = n.Tag, Prefab = n.Prefab,
                Layer = n.Layer, Sibling = n.Sibling, Active = n.Active,
                Position = n.Position, Rotation = n.Rotation, Scale = n.Scale, Id = n.Id, ParentId = n.ParentId,
                Original = (ResolveReloadObject(n.Id) as GameObject)?.transform,
                Parent = (ResolveReloadObject(n.ParentId) as GameObject)?.transform,
                Components = (n.Components ?? throw new InvalidOperationException("恢复组件列表缺失。"))
                    .Select(c => new ComponentState { TypeName = c.TypeName, Json = c.Json, Id = c.Id,
                        Original = ResolveReloadObject(c.Id) as Component }).ToList() }).ToArray();
            for (int i = 0; i < nodes.Length; i++)
            {
                var children = state.Nodes[i].Children;
                if (children == null || children.Any(c => c <= i || c >= nodes.Length))
                    throw new InvalidOperationException("恢复层级索引不完整或形成循环。");
                nodes[i].Children = children.Select(c => nodes[c]).ToList();
                if (nodes[i].Original != null && nodes[i].Original.gameObject.scene != scene
                    || nodes[i].Parent != null && nodes[i].Parent.gameObject.scene != scene)
                    throw new InvalidOperationException("恢复对象已指向另一 Scene；未执行范围恢复。");
            }
            if (state.Roots.Any(i => i < 0 || i >= nodes.Length))
                throw new InvalidOperationException("恢复根索引不完整。");
            return new CharacterTestLevelEditorOwnedSceneSnapshot(state, state.Roots.Select(i => nodes[i]).ToList());
        }

        private static Object ResolveReloadObject(ulong id)
            => id == 0 ? null : EditorUtility.EntityIdToObject(EntityId.FromULong(id));

        private CharacterTestLevelEditorOwnedSceneSnapshot(ReloadState state, List<Node> baseline)
        {
            _scene = EditorSceneManager.GetActiveScene(); _baseline = baseline;
            foreach (var added in state.Added)
            {
                var transform = (ResolveReloadObject(added.TransformObjectId) as GameObject)?.transform;
                if (added.ObjectIds == null) throw new InvalidOperationException("恢复新增对象身份记录缺失。");
                if (transform == null) continue;
                if (transform.gameObject.scene != _scene)
                    throw new InvalidOperationException("恢复新增对象范围不一致。");
                _added.Add(transform, added.ObjectIds.Select(ResolveReloadObject).ToArray());
            }
        }

        internal static CharacterTestLevelEditorOwnedSceneSnapshot Create(IReadOnlyList<IMxLevelEditorModule> modules)
            => new CharacterTestLevelEditorOwnedSceneSnapshot(Collect(modules));

        internal static CharacterTestLevelEditorOwnedSceneSnapshot CreateForOwnedTransforms(params Transform[] transforms)
            => new CharacterTestLevelEditorOwnedSceneSnapshot(transforms.ToList());

        internal void TrackOwnedAddedTransform(Transform transform)
            => _added.Add(transform, transform.GetComponentsInChildren<Component>(true).Cast<Object>().ToArray());

        private static List<Transform> Collect(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            var output = new List<Transform>();
            foreach (var module in modules)
            {
                if (module is not CharacterTestWorldObjectLevelEditorModule
                    && module is not CharacterTestSceneCharacterLevelEditorModule
                    && module is not CharacterTestGenesisValidationLevelEditorModule
                    && module is not CharacterTestGuidanceLevelEditorModule) continue;
                if (module is IMxLevelEditorSceneScope scope && !scope.AppliesToActiveScene) continue;
                if (module is not IMxLevelEditorSceneChangeAwareModule owner || owner.ManagedSceneRoot == null) continue;
                var owned = new List<Transform>(); owner.CollectManagedSceneTransforms(owned);
                foreach (var transform in owned)
                    if (transform != null && transform != owner.ManagedSceneRoot && transform.IsChildOf(owner.ManagedSceneRoot)
                        && !output.Contains(transform)) output.Add(transform);
            }
            return output;
        }

        private static ulong Id(Object value) => value == null ? 0 : EntityId.ToULong(value.GetEntityId());

        private static Node Capture(Transform transform)
        {
            var node = new Node { Original = transform, Parent = transform.parent,
                Name = transform.name, Layer = transform.gameObject.layer, Tag = transform.gameObject.tag,
                Sibling = transform.GetSiblingIndex(), Active = transform.gameObject.activeSelf,
                Position = transform.localPosition, Rotation = transform.localRotation, Scale = transform.localScale,
                Id = Id(transform.gameObject), ParentId = Id(transform.parent?.gameObject) };
            if (PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                node.Prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) throw new InvalidOperationException("登记对象含丢失脚本，不能创建安全放弃快照：" + transform.name);
                node.Components.Add(new ComponentState { Original = component, Id = Id(component),
                    TypeName = component.GetType().AssemblyQualifiedName, Json = EditorJsonUtility.ToJson(component) });
            }
            for (int i = 0; i < transform.childCount; i++) node.Children.Add(Capture(transform.GetChild(i)));
            return node;
        }

        internal void TrackAddedObjects(IReadOnlyList<IMxLevelEditorModule> modules)
        {
            if (EditorSceneManager.GetActiveScene() != _scene) return;
            foreach (var transform in Collect(modules))
            {
                if (_baseline.Any(n => n.Original == transform) || _added.ContainsKey(transform)) continue;
                // Capture immediately after registration. Later unrelated children/components
                // make deletion ambiguous and block Discard before any mutation occurs.
                _added.Add(transform, transform.GetComponentsInChildren<Component>(true).Cast<Object>().ToArray());
            }
        }

        internal bool TryRestore(IReadOnlyList<IMxLevelEditorModule> modules, out string message)
        {
            message = string.Empty;
            try
            {
                if (EditorSceneManager.GetActiveScene() != _scene)
                    throw new InvalidOperationException("关卡放弃快照不属于当前 Scene，编辑内容保留。");
                foreach (var added in _added.Where(p => p.Key != null))
                    if (added.Key.GetComponentsInChildren<Component>(true).Any(c => !added.Value.Contains(c)))
                        throw new InvalidOperationException("新增登记对象含后来添加的 Scene 组件或子物体，放弃会影响其他编辑；请先保存或移开这些对象：" + added.Key.name);
                var missing = _baseline.Where(n => n.Original == null).ToArray();
                bool changes = missing.Length > 0 || _added.Any(p => p.Key != null)
                    || _baseline.Any(n => n.Original != null && !PoseMatches(n, n.Original));
                if (!changes) { message = "关卡登记对象无需恢复；其他 Scene 编辑保留。"; return true; }
                var availableIds = AllNodes(_baseline).Select(n => n.Id).ToHashSet();
                foreach (var node in _baseline)
                    if (node.Parent == null && node.ParentId != 0 && !availableIds.Contains(node.ParentId))
                        throw new InvalidOperationException("被删除对象的结构父节点也已删除，无法安全重建；草稿保留：" + node.Name);
                Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("放弃当前关卡所属对象编辑");
                try
                {
                    var objects = new Dictionary<ulong, Object>();
                    var created = new List<(Node Node, Transform Transform)>();
                    foreach (var node in _baseline.Where(n => n.Original != null)) AddExisting(node, node.Original, objects);
                    foreach (var node in missing)
                    {
                        if (objects.ContainsKey(node.Id)) continue;
                        Create(node, null, objects, created);
                    }
                    foreach (var entry in created)
                    {
                        Transform parent = entry.Node.Parent;
                        if (parent == null && objects.TryGetValue(entry.Node.ParentId, out var parentObject)) parent = (parentObject as GameObject)?.transform;
                        entry.Transform.SetParent(parent, false);
                        ApplyPose(entry.Node, entry.Transform);
                        entry.Transform.gameObject.layer = entry.Node.Layer;
                        entry.Transform.gameObject.tag = entry.Node.Tag;
                        foreach (var component in entry.Node.Components.Where(c => Type.GetType(c.TypeName) != typeof(Transform)))
                            if (objects.TryGetValue(component.Id, out var target))
                                EditorJsonUtility.FromJsonOverwrite(Remap(component.Json, objects), target);
                        entry.Transform.gameObject.SetActive(entry.Node.Active);
                    }
                    foreach (var node in _baseline.Where(n => n.Original != null))
                    {
                        if (PoseMatches(node, node.Original)) continue;
                        Undo.RecordObject(node.Original, "恢复关卡登记对象位置");
                        Undo.RecordObject(node.Original.gameObject, "恢复关卡登记对象名称");
                        Transform parent = node.Parent;
                        if (parent == null && objects.TryGetValue(node.ParentId, out var parentObject)) parent = (parentObject as GameObject)?.transform;
                        if (node.Original.parent != parent) Undo.SetTransformParent(node.Original, parent, "恢复关卡登记对象父节点");
                        ApplyPose(node, node.Original);
                    }
                    foreach (var added in _added.Where(p => p.Key != null).Select(p => p.Key).ToArray())
                        if (!_added.Keys.Any(parent => parent != null && parent != added && added.IsChildOf(parent)))
                            Undo.DestroyObjectImmediate(added.gameObject);
                    Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undoGroup);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    message = "已放弃当前关卡所属对象编辑；其他 Scene 组件与对象编辑保留。";
                    return true;
                }
                catch { Undo.RevertAllDownToGroup(undoGroup); throw; }
            }
            catch (Exception error) { message = error.Message; return false; }
        }

        private static IEnumerable<Node> AllNodes(IEnumerable<Node> roots)
        {
            foreach (var node in roots) { yield return node; foreach (var child in AllNodes(node.Children)) yield return child; }
        }

        private static void AddExisting(Node node, Transform transform, Dictionary<ulong, Object> objects)
        {
            objects[node.Id] = transform.gameObject;
            foreach (var component in node.Components) if (component.Original != null) objects[component.Id] = component.Original;
            foreach (var child in node.Children) if (child.Original != null) AddExisting(child, child.Original, objects);
        }

        private static void Create(Node node, Transform parent, Dictionary<ulong, Object> objects,
            List<(Node Node, Transform Transform)> created)
        {
            GameObject gameObject = !string.IsNullOrEmpty(node.Prefab)
                ? PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(node.Prefab)) as GameObject : new GameObject(node.Name);
            if (gameObject == null) throw new InvalidOperationException("无法恢复登记对象：" + node.Name);
            Undo.RegisterCreatedObjectUndo(gameObject, "恢复被删除的关卡登记对象");
            gameObject.transform.SetParent(parent, false); objects[node.Id] = gameObject;
            created.Add((node, gameObject.transform));
            var used = new Dictionary<Type, int>();
            foreach (var component in node.Components)
            {
                Type type = Type.GetType(component.TypeName) ?? throw new InvalidOperationException("恢复所需组件类型不可用：" + component.TypeName);
                int index = used.TryGetValue(type, out int count) ? count : 0; used[type] = index + 1;
                var existing = gameObject.GetComponents(type);
                Component value = index < existing.Length ? existing[index] : Undo.AddComponent(gameObject, type);
                objects[component.Id] = value;
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (!string.IsNullOrEmpty(node.Prefab) && i < gameObject.transform.childCount)
                {
                    var transform = gameObject.transform.GetChild(i); created.Add((child, transform));
                    objects[child.Id] = transform.gameObject;
                    MapComponents(child, transform, objects, created);
                }
                else Create(child, gameObject.transform, objects, created);
            }
        }

        private static void MapComponents(Node node, Transform transform, Dictionary<ulong, Object> objects,
            List<(Node Node, Transform Transform)> created)
        {
            var used = new Dictionary<Type, int>();
            foreach (var component in node.Components)
            {
                Type type = Type.GetType(component.TypeName); int index = used.TryGetValue(type, out int count) ? count : 0; used[type] = index + 1;
                var available = transform.GetComponents(type);
                objects[component.Id] = index < available.Length ? available[index] : Undo.AddComponent(transform.gameObject, type);
            }
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (i >= transform.childCount) Create(child, transform, objects, created);
                else
                {
                    var target = transform.GetChild(i); objects[child.Id] = target.gameObject;
                    created.Add((child, target)); MapComponents(child, target, objects, created);
                }
            }
        }

        private static string Remap(string json, Dictionary<ulong, Object> objects)
        {
            var document = JObject.Parse(json);
            foreach (var property in document.Descendants().OfType<JProperty>())
                if ((property.Name == "instanceID" || property.Name == "entityId") && property.Value.Type == JTokenType.Integer
                    && ulong.TryParse(property.Value.ToString(), out var id) && objects.TryGetValue(id, out var value)) property.Value = Id(value);
            return document.ToString();
        }

        private static bool PoseMatches(Node node, Transform transform)
            => transform.parent == node.Parent && transform.name == node.Name
                && transform.localPosition == node.Position && transform.localRotation == node.Rotation
                && transform.localScale == node.Scale && transform.GetSiblingIndex() == node.Sibling;

        private static void ApplyPose(Node node, Transform transform)
        {
            transform.name = node.Name; transform.localPosition = node.Position;
            transform.localRotation = node.Rotation; transform.localScale = node.Scale;
            transform.SetSiblingIndex(node.Sibling);
        }
    }
}
