using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed class TacticalBattlePreparationState
    {
        public const string CurrentSchema = "mx.character-test.tactical-battle-preparation.v2";
        public const string CharacterItemSchema = "mx.character-test.tactical-battle-preparation.v3";
        public string Schema { get; set; } = CurrentSchema;
        public ulong NextEntityId { get; set; } = 1;
        public string ActivePresetId { get; set; } = string.Empty;
        public TacticalBattleSetup Working { get; set; } = new TacticalBattleSetup();
        public List<TacticalBattlePreset> Presets { get; set; } = new List<TacticalBattlePreset>();
    }

    internal sealed class TacticalBattleSetup
    {
        public string SceneId { get; set; } = "tactical-card-demo";
        public string PlayerControlledEntityId { get; set; } = string.Empty;
        public List<TacticalBattleEntitySetup> Entities { get; set; } = new List<TacticalBattleEntitySetup>();
    }

    internal sealed class TacticalBattleEntitySetup
    {
        public string EntityId { get; set; } = string.Empty;
        public int CharacterId { get; set; }
        public string CharacterStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int TeamId { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; } = 1f;
        public float Z { get; set; }
        public float YawDegrees { get; set; }
        public ulong NextCardInstanceId { get; set; } = 1;
        public CharacterTacticalDefaults Loadout { get; set; } = new CharacterTacticalDefaults();

        // Saved references only. Cards, learned actions and bindings stay with the role item.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int? CharacterItemInstanceId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int? InventoryOwnerId { get; set; }
        [JsonIgnore] public long CharacterItemRevision { get; set; }
        [JsonIgnore] public Vector3 Position => new Vector3(X, Y, Z);
    }

    internal sealed class TacticalBattlePreset
    {
        public string PresetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TacticalBattleSetup Snapshot { get; set; } = new TacticalBattleSetup();
    }

    /// <summary>Owns the editable whole-battle setup and named snapshots. Mutations are low-frequency preparation work.</summary>
    internal sealed class TacticalBattlePreparationService
    {
        private readonly CharacterTacticalPlayerService _player;
        private readonly ICharacterTacticalStateStore _store;
        private readonly CharacterTestCharacterDefinition[] _definitions;
        private TacticalBattlePreparationState _state;
        private Task<bool> _saving;
        private long _savedRevision;
        private bool _initializing;

        private TacticalBattlePreparationService(CharacterTacticalPlayerService player, ICharacterTacticalStateStore store,
            CharacterTestCharacterDefinition[] definitions)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _definitions = definitions ?? CharacterTestPlayableCharacterCatalog.Discover().ToArray();
        }

        internal long Revision { get; private set; }
        internal bool HasUnsavedChanges => Revision != _savedRevision;
        internal string SaveIssue { get; private set; } = string.Empty;
        internal TacticalBattleSetup Working
        {
            get
            {
                if (!TryCaptureWorking(out var setup, out string issue)) throw new InvalidOperationException(issue);
                return setup;
            }
        }

        internal bool TryCaptureWorking(out TacticalBattleSetup setup, out string issue)
            => TryProjectSetup(_state.Working, out setup, out issue);

        private bool TryProjectSetup(TacticalBattleSetup source, out TacticalBattleSetup setup, out string issue)
        {
            setup = Clone(source); issue = string.Empty;
            var selected = new HashSet<int>();
            foreach (var entity in setup.Entities)
            {
                if (!entity.CharacterItemInstanceId.HasValue && !entity.InventoryOwnerId.HasValue)
                {
                    if (_player.UsesCharacterItemTacticalProgress) return Fail("旧整备仍引用角色模板；需要明确选择角色物品实例。", out issue);
                    continue;
                }
                if (!entity.CharacterItemInstanceId.HasValue || !entity.InventoryOwnerId.HasValue
                    || !selected.Add(entity.CharacterItemInstanceId.Value))
                    return Fail("出战角色实例引用重复或不完整。", out issue);
                var id = new ItemInstanceId(entity.CharacterItemInstanceId.Value);
                var owner = new InventoryOwnerId(entity.InventoryOwnerId.Value);
                if (!_player.TryReadCharacterItemLoadout(id, owner, out var loadout, out long revision, out issue)) return false;
                if (loadout.CharacterStableId != entity.CharacterStableId
                    || !_definitions.Any(value => value.CharacterId == entity.CharacterId
                        && value.TacticalDefaults?.CharacterStableId == entity.CharacterStableId))
                    return Fail("角色实例与出战模板引用不一致。", out issue);
                if (!_player.TryReadCharacterItem(id, owner, out var record, out issue)) return false;
                entity.Loadout = loadout; entity.CharacterItemRevision = revision; entity.DisplayName = record.DisplayName;
            }
            return true;
        }

        internal bool AddCharacterItemEntity(ItemInstanceId id, InventoryOwnerId owner, Vector3 position,
            float yawDegrees, int teamId, long expectedRevision, out string entityId, out string issue)
        {
            entityId = string.Empty;
            if (expectedRevision != Revision) return Fail("整备版本已变化。", out issue);
            if (!ValidPose(position, yawDegrees) || teamId <= 0) return Fail("阵营或角色位置无效。", out issue);
            if (!_player.TryReadCharacterItemLoadout(id, owner, out var loadout, out _, out issue)) return false;
            var definitions = _definitions.Where(value => value.TacticalDefaults?.CharacterStableId == loadout.CharacterStableId).ToArray();
            if (definitions.Length != 1) return Fail("角色模板定义缺失或不唯一。", out issue);
            if (_state.Working.Entities.Any(value => value.CharacterItemInstanceId == id.Value))
                return Fail("同一个角色物品已经被选择出战。", out issue);
            if (_state.NextEntityId == 0 || _state.NextEntityId == ulong.MaxValue) return Fail("整备实体编号已耗尽。", out issue);
            var candidate = Clone(_state);
            entityId = "entity-" + candidate.NextEntityId++.ToString("D6");
            candidate.Schema = TacticalBattlePreparationState.CharacterItemSchema;
            candidate.Working.Entities.Add(new TacticalBattleEntitySetup { EntityId = entityId,
                CharacterId = definitions[0].CharacterId, CharacterStableId = loadout.CharacterStableId,
                DisplayName = string.Empty, CharacterItemInstanceId = id.Value, InventoryOwnerId = owner.Value,
                TeamId = teamId, X = position.x, Y = position.y, Z = position.z, YawDegrees = NormalizeYaw(yawDegrees),
                Loadout = new CharacterTacticalDefaults { CharacterStableId = loadout.CharacterStableId } });
            _state = candidate; Touch(); issue = string.Empty; return true;
        }

        internal bool SelectCharacterItem(string entityId, ItemInstanceId id, InventoryOwnerId owner,
            long expectedRevision, out string issue)
        {
            if (expectedRevision != Revision) return Fail("整备版本已变化。", out issue);
            var entity = FindStored(entityId);
            if (entity == null) return Fail("场景实体不存在。", out issue);
            if (_state.Working.Entities.Any(value => value.EntityId != entityId && value.CharacterItemInstanceId == id.Value))
                return Fail("同一个角色物品已经被选择出战。", out issue);
            if (!_player.TryReadCharacterItemLoadout(id, owner, out var loadout, out _, out issue)) return false;
            var definitions = _definitions.Where(value => value.TacticalDefaults?.CharacterStableId == loadout.CharacterStableId).ToArray();
            if (definitions.Length != 1) return Fail("角色模板定义缺失或不唯一。", out issue);
            if (entity.CharacterItemInstanceId == id.Value && entity.InventoryOwnerId == owner.Value) return true;
            var candidate = Clone(_state); entity = candidate.Working.Entities.Find(value => value.EntityId == entityId);
            entity.CharacterId = definitions[0].CharacterId; entity.CharacterStableId = loadout.CharacterStableId;
            entity.CharacterItemInstanceId = id.Value; entity.InventoryOwnerId = owner.Value; entity.DisplayName = string.Empty;
            entity.Loadout = new CharacterTacticalDefaults { CharacterStableId = loadout.CharacterStableId };
            entity.NextCardInstanceId = 1; candidate.Schema = TacticalBattlePreparationState.CharacterItemSchema;
            _state = candidate; Touch(); issue = string.Empty; return true;
        }

        private static bool ValidPose(Vector3 position, float yaw) => !float.IsNaN(yaw) && !float.IsInfinity(yaw)
            && !float.IsNaN(position.x) && !float.IsNaN(position.y) && !float.IsNaN(position.z)
            && !float.IsInfinity(position.x) && !float.IsInfinity(position.y) && !float.IsInfinity(position.z)
            && TacticalBattlefieldLayout.ContainsPlacement(position);
        internal string CaptureProgressJson() => JsonConvert.SerializeObject(_state);

        internal bool ConfigureNewGame(string playerCharacter, string opponentCharacter, out string issue, IReadOnlyList<string> opponentCardIds)
        {
            var player = _definitions.Where(value => value.TacticalDefaults?.CharacterStableId == playerCharacter).ToArray();
            var opponent = _definitions.Where(value => value.TacticalDefaults?.CharacterStableId == opponentCharacter).ToArray();
            if (player.Length != 1 || opponent.Length != 1)
                return Fail("初始玩家或对手角色定义缺失/不唯一。", out issue);
            var previous = _state;
            bool priorInitializing = _initializing;
            _state = new TacticalBattlePreparationState();
            _initializing = true;
            try
            {
                if (!AddEntity(player[0].CharacterId, new Vector3(-2f, 1f, -1f), 45f, 1, out string playerEntity, out issue)
                    || !AddEntity(opponent[0].CharacterId, new Vector3(2f, 1f, 1f), 225f, 2, out string opponentEntity, out issue))
                { _state = previous; return false; }
                // Authored first-encounter deck is separate from player ownership grants.
                var opponentSetup = FindStored(opponentEntity);
                if (opponentCardIds == null || opponentCardIds.Count == 0
                    || opponentCardIds.Distinct(StringComparer.Ordinal).Count() != opponentCardIds.Count
                    || opponentCardIds.Any(id => !opponentSetup.Loadout.Cards.Any(card => card.CardId == id)))
                { _state = previous; return Fail("初始对手卡组未配置或未获得对应正式卡。", out issue); }
                foreach (var card in opponentSetup.Loadout.Cards.ToArray())
                    if (!opponentCardIds.Contains(card.CardId) && !RemoveCard(opponentEntity, card.InstanceId, out issue))
                    { _state = previous; return false; }
                _state.Working.PlayerControlledEntityId = playerEntity;
                if (!ValidateForBattle(out issue)) { _state = previous; return false; }
            }
            catch { _state = previous; throw; }
            finally { _initializing = priorInitializing; }
            Touch(); issue = string.Empty; return true;
        }

        // Build only an unpublished, empty new-profile preparation. On failure restore the detached previous state.
        internal bool ConfigureCharacterItemNewGame(CharacterTacticalRoleNewGameConfig recipe,
            IReadOnlyDictionary<long, ItemInstanceId> instances, out string issue)
        {
            issue = string.Empty;
            if (!_player.UsesCharacterItemTacticalProgress || recipe == null || instances == null
                || _state.Working.Entities.Count != 0 || _state.Presets.Count != 0)
                return Fail("角色物品初始整备必须使用空的新档候选。", out issue);
            var previous = _state; long priorRevision = Revision; bool priorInitializing = _initializing;
            _state = new TacticalBattlePreparationState { Schema = TacticalBattlePreparationState.CharacterItemSchema };
            _initializing = true;
            try
            {
                foreach (var grant in recipe.CharacterItemGrants.OrderBy(row => row.RequestId))
                {
                    if (!instances.TryGetValue(grant.RequestId, out var instance))
                    { _state = previous; Revision = priorRevision; return Fail("初始角色获取结果缺失。", out issue); }
                    var deployment = grant.Deployment;
                    if (deployment == null) continue;
                    if (!AddCharacterItemEntity(instance, new InventoryOwnerId(grant.OwnerId),
                        new Vector3(deployment.X, deployment.Y, deployment.Z), deployment.YawDegrees,
                        deployment.TeamId, Revision, out string entity, out issue))
                    { _state = previous; Revision = priorRevision; return false; }
                    if (grant.RequestId == recipe.PlayerControlledGrantRequestId)
                        _state.Working.PlayerControlledEntityId = entity;
                }
                if (string.IsNullOrEmpty(_state.Working.PlayerControlledEntityId) || !ValidateForBattle(out issue))
                { _state = previous; Revision = priorRevision; return Fail("初始实例整备无效：" + issue, out issue); }
            }
            catch { _state = previous; Revision = priorRevision; throw; }
            finally { _initializing = priorInitializing; }
            Touch(); issue = string.Empty; return true;
        }

        internal bool EnsurePlayerControlledEntity(out string issue)
        {
            if (!TryCaptureWorking(out var setup, out issue)) return false;
            if (setup.Entities.Any(entity => entity.EntityId == setup.PlayerControlledEntityId
                && entity.Loadout?.Cards?.Count > 0)) { issue = string.Empty; return true; }
            TacticalBattleEntitySetup first = setup.Entities.FirstOrDefault(entity => entity.Loadout?.Cards?.Count > 0);
            if (first == null) { issue = "请为出战角色装配至少一张正式行为卡。"; return false; }
            return SetPlayerControlledEntity(first.EntityId, out issue);
        }
        internal IReadOnlyList<TacticalBattlePreset> Presets => _state.Presets;
        internal string ActivePresetId => _state.ActivePresetId ?? string.Empty;
        internal bool WorkingDiffersFromActivePreset
        {
            get
            {
                TacticalBattlePreset preset = _state.Presets.Find(value => value.PresetId == _state.ActivePresetId);
                return preset == null || JsonConvert.SerializeObject(_state.Working) != JsonConvert.SerializeObject(preset.Snapshot);
            }
        }
        internal event Action Changed;

        internal static async Task<TacticalBattlePreparationService> OpenAsync(
            CharacterTacticalPlayerService player,
            ICharacterTacticalStateStore store,
            CharacterTestCharacterDefinition[] definitions = null)
        {
            var service = new TacticalBattlePreparationService(player, store, definitions);
            string json = await store.LoadAsync();
            service._state = string.IsNullOrWhiteSpace(json)
                ? service.CreateInitialState()
                : JsonConvert.DeserializeObject<TacticalBattlePreparationState>(json);
            bool migrated = service.Migrate();
            service.Validate();
            bool repairedDefaults = service.EnsureDefaultBindings(service._state.Working);
            for (int i = 0; i < service._state.Presets.Count; i++)
                repairedDefaults |= service.EnsureDefaultBindings(service._state.Presets[i].Snapshot);
            if (json == null || migrated || repairedDefaults)
            {
                service.Revision++;
                await service.FlushAsync();
            }
            return service;
        }

        private TacticalBattleEntitySetup FindStored(string entityId) =>
            _state.Working.Entities.Find(value => value.EntityId == entityId);

        // Existing legacy edits keep their stored entity. Instance display reads get only an owner projection.
        internal TacticalBattleEntitySetup Find(string entityId)
        {
            var entity = FindStored(entityId);
            if (entity == null || !entity.CharacterItemInstanceId.HasValue) return entity;
            var source = new TacticalBattleSetup { Entities = new List<TacticalBattleEntitySetup> { entity } };
            return TryProjectSetup(source, out var projected, out _) ? projected.Entities[0] : null;
        }

        internal bool AddEntity(int characterId, Vector3 position, float yawDegrees, int teamId,
            out string entityId, out string issue)
        {
            entityId = string.Empty;
            if (!TacticalBattlefieldLayout.ContainsPlacement(position))
                return Fail("角色位置超出露天战区。", out issue);
            if (teamId <= 0) return Fail("阵营无效。", out issue);
            CharacterTestCharacterDefinition definition = _definitions
                .FirstOrDefault(value => value.CharacterId == characterId);
            if (definition.CharacterId == 0 || definition.TacticalDefaults == null)
                return Fail("角色模板没有可用战术配置。", out issue);
            CharacterTacticalDefaults source = _player.Read(definition.TacticalDefaults.CharacterStableId);
            if (source == null) return Fail("角色卡组尚未加载。", out issue);
            entityId = "entity-" + _state.NextEntityId++.ToString("D6");
            CharacterTacticalDefaults loadout = Clone(source);
            // New scene entities are AI until explicitly selected. Keep player ownership intact;
            // only this new deployment copy excludes cards without a production automatic owner.
            if (!_initializing)
            {
                var manual = new HashSet<ulong>(loadout.Cards.Where(card => _player.Catalog.ExactCard(card)?.PointMove != null)
                    .Select(card => card.InstanceId));
                loadout.Cards.RemoveAll(card => manual.Contains(card.InstanceId));
                loadout.Bindings.RemoveAll(binding => manual.Contains(binding.CardInstanceId));
            }
            NormalizeLoadout(loadout);
            var entity = new TacticalBattleEntitySetup
            {
                EntityId = entityId,
                CharacterId = characterId,
                CharacterStableId = definition.TacticalDefaults.CharacterStableId,
                DisplayName = NextDisplayName(definition.DisplayName),
                TeamId = teamId,
                X = position.x,
                Y = position.y,
                Z = position.z,
                YawDegrees = NormalizeYaw(yawDegrees),
                Loadout = loadout,
                NextCardInstanceId = Math.Max(1UL, loadout.Cards.Count == 0 ? 1UL : loadout.Cards.Max(card => card.InstanceId) + 1UL)
            };
            EnsureDefaultBindings(entity.Loadout, entity.CharacterStableId);
            _state.Working.Entities.Add(entity);
            Touch(); issue = string.Empty; return true;
        }

        internal bool RemoveEntity(string entityId, out string issue)
        {
            if (_state.Working.Entities.RemoveAll(value => value.EntityId == entityId) == 0)
                return Fail("场景实体不存在。", out issue);
            if (_state.Working.PlayerControlledEntityId == entityId)
                _state.Working.PlayerControlledEntityId = string.Empty;
            Touch(); issue = string.Empty; return true;
        }

        internal bool SetPlayerControlledEntity(string entityId, out string issue)
        {
            string value = entityId ?? string.Empty;
            if (value.Length > 0 && _state.Working.Entities.All(entity => entity.EntityId != value))
                return Fail("场景实体不存在。", out issue);
            if (_state.Working.PlayerControlledEntityId == value) { issue = string.Empty; return true; }
            if (!ValidateAutomaticCards(value, out issue)) return false;
            _state.Working.PlayerControlledEntityId = value;
            Touch(); issue = string.Empty; return true;
        }

        internal bool SetPose(string entityId, Vector3 position, float yawDegrees, out string issue)
        {
            TacticalBattleEntitySetup entity = FindStored(entityId);
            if (entity == null) return Fail("场景实体不存在。", out issue);
            if (!TacticalBattlefieldLayout.ContainsPlacement(position))
                return Fail("角色位置超出露天战区。", out issue);
            entity.X = position.x; entity.Y = position.y; entity.Z = position.z;
            entity.YawDegrees = NormalizeYaw(yawDegrees); Touch(); issue = string.Empty; return true;
        }

        internal bool SetTeam(string entityId, int teamId, out string issue)
        {
            TacticalBattleEntitySetup entity = FindStored(entityId);
            if (entity == null || teamId <= 0) return Fail("实体或阵营无效。", out issue);
            if (entity.TeamId == teamId) { issue = string.Empty; return true; }
            entity.TeamId = teamId; Touch(); issue = string.Empty; return true;
        }

        internal bool AddCard(string entityId, CharacterTacticalCardReference published, out ulong instanceId, out string issue)
        {
            instanceId = 0;
            TacticalBattleEntitySetup entity = FindStored(entityId);
            CharacterTacticalCardDefinition card = _player.Catalog.ExactCard(published);
            if (entity == null || card == null) return Fail("实体或行为卡版本无效。", out issue);
            if (entity.CharacterItemInstanceId.HasValue) return Fail("角色实例卡组由角色物品持有，须使用实例命令。", out issue);
            if (card.PointMove != null && _state.Working.PlayerControlledEntityId != entityId)
                return Fail(ManualPointIssue(entity, card), out issue);
            if (!(_player.Read(entity.CharacterStableId)?.Cards.Any(owned => owned.CardId == published.CardId
                && owned.Revision == published.Revision && owned.ContentHash == published.ContentHash) ?? false))
                return Fail("该角色尚未获得这张正式行为卡。发布卡不会自动授予玩家。", out issue);
            if (entity.NextCardInstanceId == 0 || entity.NextCardInstanceId == ulong.MaxValue)
                return Fail("卡牌实例编号已耗尽。", out issue);
            instanceId = entity.NextCardInstanceId++;
            var added = new CharacterTacticalCardReference
            {
                InstanceId = instanceId, CardId = published.CardId,
                Revision = published.Revision, ContentHash = published.ContentHash
            };
            entity.Loadout.Cards.Add(added);
            EnsureDefaultBinding(entity.Loadout, entity.CharacterStableId, added);
            Touch(); issue = string.Empty; return true;
        }

        internal bool RemoveCard(string entityId, ulong instanceId, out string issue)
        {
            TacticalBattleEntitySetup entity = FindStored(entityId);
            if (entity == null) return Fail("场景实体不存在。", out issue);
            if (entity.CharacterItemInstanceId.HasValue) return Fail("角色实例装配须使用实例命令。", out issue);
            if (entity.Loadout.Cards.RemoveAll(card => card.InstanceId == instanceId) == 0)
                return Fail("卡牌实例不存在。", out issue);
            entity.Loadout.Bindings.RemoveAll(binding => binding.CardInstanceId == instanceId);
            Touch(); issue = string.Empty; return true;
        }

        internal bool BindAction(string entityId, ulong instanceId, string actionId, out string issue)
        {
            TacticalBattleEntitySetup entity = FindStored(entityId);
            if (entity == null) return Fail("场景实体不存在。", out issue);
            if (entity.CharacterItemInstanceId.HasValue) return Fail("角色实例装配须使用实例命令。", out issue);
            CharacterTacticalCardReference reference = entity.Loadout.Cards.Find(card => card.InstanceId == instanceId);
            CharacterTacticalCardDefinition cardDefinition = _player.Catalog.ExactCard(reference);
            if (cardDefinition?.HasActionSlot != true) return Fail("该行为卡没有动作槽。", out issue);
            CharacterTacticalDefaults owned = _player.Read(entity.CharacterStableId);
            CharacterTacticalActionDefinition action = _player.Catalog.Action(actionId);
            if (owned == null || action == null || !_player.Catalog.ContainsAction(owned.ActionIds, actionId)
                || !action.CharacterStableIds.Contains(entity.CharacterStableId))
                return Fail("该实体没有此可用动作。", out issue);
            CharacterTacticalSlotBinding binding = entity.Loadout.Bindings.Find(value => value.CardInstanceId == instanceId);
            if (binding == null)
                entity.Loadout.Bindings.Add(new CharacterTacticalSlotBinding
                {
                    CardInstanceId = instanceId, CardId = reference.CardId,
                    SlotPath = cardDefinition.ActionSlotPath, TacticalActionId = actionId
                });
            else
            {
                binding.SlotPath = cardDefinition.ActionSlotPath;
                binding.TacticalActionId = actionId;
            }
            Touch(); issue = string.Empty; return true;
        }

        internal bool SaveAsPreset(string name, out string presetId, out string issue)
        {
            presetId = string.Empty;
            string title = name?.Trim() ?? string.Empty;
            if (title.Length == 0) return Fail("预设名称不能为空。", out issue);
            presetId = "preset-" + Guid.NewGuid().ToString("N");
            _state.Presets.Add(new TacticalBattlePreset { PresetId = presetId, Name = title, Snapshot = Clone(_state.Working) });
            _state.ActivePresetId = presetId; Touch(); issue = string.Empty; return true;
        }

        internal bool UpdateActivePreset(out string issue)
        {
            TacticalBattlePreset preset = _state.Presets.Find(value => value.PresetId == _state.ActivePresetId);
            if (preset == null) return Fail("当前没有可更新的命名预设。", out issue);
            preset.Snapshot = Clone(_state.Working); Touch(); issue = string.Empty; return true;
        }

        internal bool LoadPreset(string presetId, out string issue)
        {
            TacticalBattlePreset preset = _state.Presets.Find(value => value.PresetId == presetId);
            if (preset == null) return Fail("预设不存在。", out issue);
            if ((_player.UsesCharacterItemTacticalProgress || preset.Snapshot.Entities.Any(value => value.CharacterItemInstanceId.HasValue))
                && !TryProjectSetup(preset.Snapshot, out _, out issue)) return false;
            _state.Working = Clone(preset.Snapshot);
            _state.ActivePresetId = preset.PresetId; Touch(); issue = string.Empty; return true;
        }

        internal bool ValidateForBattle(out string issue)
        {
            if (!TryCaptureWorking(out var setup, out issue)) return false;
            if (!ValidateAutomaticCards(setup, setup.PlayerControlledEntityId, out issue)) return false;
            if (setup.Entities.Count == 0) return Fail("请先放置出战实体。", out issue);
            if (!setup.Entities.Any(entity => entity.TeamId == 1)
                || !setup.Entities.Any(entity => entity.TeamId == 2))
                return Fail("阵营 A 和阵营 B 都至少需要一个实体。", out issue);
            if (!string.IsNullOrEmpty(setup.PlayerControlledEntityId)
                && !setup.Entities.Any(entity => entity.EntityId == setup.PlayerControlledEntityId))
                return Fail("玩家控制实体已经失效。", out issue);
            TacticalBattleEntitySetup playerEntity = setup.Entities.FirstOrDefault(entity =>
                entity.EntityId == setup.PlayerControlledEntityId);
            if (playerEntity != null && playerEntity.Loadout.Cards.Count == 0)
                return Fail(playerEntity.DisplayName + " 由玩家控制，至少需要一张行为卡。", out issue);
            foreach (TacticalBattleEntitySetup entity in setup.Entities)
            {
                if (!TacticalBattlefieldLayout.ContainsPlacement(entity.Position))
                    return Fail(entity.DisplayName + " 位于露天战区外。", out issue);
                foreach (CharacterTacticalCardReference card in entity.Loadout.Cards)
                {
                    CharacterTacticalCardDefinition definition = _player.Catalog.ExactCard(card);
                    if (definition == null) return Fail(entity.DisplayName + " 存在失效行为卡。", out issue);
                    if (entity.EntityId == setup.PlayerControlledEntityId && definition.PresentationSource != null
                        && !definition.PresentationSource.TryValidatePlayerExecution(out string executionIssue))
                        return Fail(entity.DisplayName + " 的“" + definition.DisplayName + "”：" + executionIssue, out issue);
                    if (entity.CharacterItemInstanceId.HasValue && !_player.TryGetCharacterItemBindings(
                            new ItemInstanceId(entity.CharacterItemInstanceId.Value), new InventoryOwnerId(entity.InventoryOwnerId.Value),
                            card.InstanceId, out _, out issue)) return false;
                    if (definition.HasActionSlot && !entity.Loadout.Bindings.Any(binding =>
                            binding.CardInstanceId == card.InstanceId && !string.IsNullOrWhiteSpace(binding.TacticalActionId)))
                        return Fail(entity.DisplayName + " 的“" + definition.DisplayName + "”尚未配置动作。", out issue);
                }
            }
            issue = string.Empty; return true;
        }

        private bool ValidateAutomaticCards(string playerEntityId, out string issue)
        {
            if (!TryCaptureWorking(out var setup, out issue)) return false;
            return ValidateAutomaticCards(setup, playerEntityId, out issue);
        }

        private bool ValidateAutomaticCards(TacticalBattleSetup setup, string playerEntityId, out string issue)
        {
            foreach (var entity in setup.Entities)
                if (entity.EntityId != playerEntityId)
                    foreach (var reference in entity.Loadout.Cards)
                    {
                        var card = _player.Catalog.ExactCard(reference);
                        if (card?.PointMove != null) return Fail(ManualPointIssue(entity, card), out issue);
                    }
            issue = string.Empty; return true;
        }

        private static string ManualPointIssue(TacticalBattleEntitySetup entity, CharacterTacticalCardDefinition card)
            => entity.DisplayName + " 的“" + card.DisplayName + "”需要玩家手动选点。请先从该实体的出战卡组移除此卡，或保持该实体为玩家控制；角色持有卡不会被删除。";

        private bool Migrate()
        {
            if (_state?.Schema == TacticalBattlePreparationState.CurrentSchema
                || _state?.Schema == TacticalBattlePreparationState.CharacterItemSchema) return false;
            if (_state?.Schema != "mx.character-test.tactical-battle-preparation.v1")
                throw new JsonSerializationException("TACTICAL_BATTLE_PREPARATION_SCHEMA_UNSUPPORTED");
            _state.Schema = TacticalBattlePreparationState.CurrentSchema;
            if (_state.Working != null) _state.Working.PlayerControlledEntityId ??= string.Empty;
            for (int i = 0; i < (_state.Presets?.Count ?? 0); i++)
                if (_state.Presets[i]?.Snapshot != null)
                    _state.Presets[i].Snapshot.PlayerControlledEntityId ??= string.Empty;
            return true;
        }

        internal Task<bool> FlushAsync()
        {
            if (_saving != null && !_saving.IsCompleted) return _saving;
            _saving = SaveLoopAsync(); return _saving;
        }

        private async Task<bool> SaveLoopAsync()
        {
            try
            {
                while (HasUnsavedChanges)
                {
                    long revision = Revision;
                    await _store.SaveAsync(JsonConvert.SerializeObject(_state));
                    _savedRevision = revision;
                }
                SaveIssue = string.Empty; return true;
            }
            catch (Exception error) { SaveIssue = "尚未保存：" + error.Message; return false; }
            finally { Changed?.Invoke(); }
        }

        private TacticalBattlePreparationState CreateInitialState()
        {
            var state = new TacticalBattlePreparationState();
            _state = state;
            _initializing = true;
            if (_player.UsesCharacterItemTacticalProgress)
            { state.Schema = TacticalBattlePreparationState.CharacterItemSchema; _initializing = false; return state; }
            CharacterTestCharacterDefinition[] definitions = _definitions
                .Where(value => value.TacticalDefaults != null).Take(2).ToArray();
            if (definitions.Length > 0) AddEntity(definitions[0].CharacterId, new Vector3(-2f, 1f, -1f), 45f, 1, out _, out _);
            if (definitions.Length > 1) AddEntity(definitions[1].CharacterId, new Vector3(2f, 1f, 1f), 225f, 2, out _, out _);
            _state.Working.PlayerControlledEntityId = _state.Working.Entities.FirstOrDefault(entity => entity.Loadout.Cards.Count > 0)?.EntityId ?? string.Empty;
            _initializing = false;
            Revision = 0;
            return state;
        }

        private string NextDisplayName(string templateName)
        {
            int count = _state.Working.Entities.Count(entity => entity.DisplayName.StartsWith(templateName ?? string.Empty, StringComparison.Ordinal)) + 1;
            return (string.IsNullOrWhiteSpace(templateName) ? "角色" : templateName) + " " + count.ToString("D2");
        }

        private void NormalizeLoadout(CharacterTacticalDefaults loadout)
        {
            if (loadout?.Cards == null || loadout.Bindings == null) return;
            var normalized = new List<CharacterTacticalSlotBinding>();
            foreach (CharacterTacticalCardReference card in loadout.Cards)
            {
                CharacterTacticalCardDefinition definition = _player.Catalog.Card(card);
                if (definition?.HasActionSlot != true) continue;
                CharacterTacticalSlotBinding source = loadout.Bindings.FirstOrDefault(binding =>
                    binding.CardInstanceId == card.InstanceId && binding.SlotPath == definition.ActionSlotPath)
                    ?? loadout.Bindings.FirstOrDefault(binding => binding.CardInstanceId == card.InstanceId);
                if (source == null) continue;
                normalized.Add(new CharacterTacticalSlotBinding
                {
                    CardInstanceId = card.InstanceId,
                    CardId = card.CardId,
                    SlotPath = definition.ActionSlotPath,
                    TacticalActionId = source.TacticalActionId
                });
            }
            loadout.Bindings = normalized;
        }

        private bool EnsureDefaultBindings(TacticalBattleSetup setup)
        {
            if (setup?.Entities == null) return false;
            bool changed = false;
            for (int i = 0; i < setup.Entities.Count; i++)
            {
                TacticalBattleEntitySetup entity = setup.Entities[i];
                if (entity?.Loadout == null || entity.CharacterItemInstanceId.HasValue) continue;
                changed |= EnsureDefaultBindings(entity.Loadout, entity.CharacterStableId);
            }
            return changed;
        }

        private bool EnsureDefaultBindings(CharacterTacticalDefaults loadout, string characterStableId)
        {
            if (loadout?.Cards == null || loadout.Bindings == null || loadout.ActionIds == null) return false;
            bool changed = false;
            for (int i = 0; i < loadout.Cards.Count; i++)
                changed |= EnsureDefaultBinding(loadout, characterStableId, loadout.Cards[i]);
            return changed;
        }

        private bool EnsureDefaultBinding(CharacterTacticalDefaults loadout, string characterStableId,
            CharacterTacticalCardReference reference)
        {
            CharacterTacticalCardDefinition card = _player.Catalog.ExactCard(reference);
            if (card?.HasActionSlot != true) return false;
            CharacterTacticalSlotBinding binding = loadout.Bindings.Find(value =>
                value.CardInstanceId == reference.InstanceId);
            CharacterTacticalActionDefinition current = binding == null
                ? null
                : _player.Catalog.Action(binding.TacticalActionId);
            if (current != null && _player.Catalog.ContainsAction(loadout.ActionIds, current.Id)
                && current.CharacterStableIds.Contains(characterStableId))
            {
                if (binding.SlotPath == card.ActionSlotPath && binding.CardId == reference.CardId) return false;
                binding.SlotPath = card.ActionSlotPath; binding.CardId = reference.CardId; return true;
            }
            CharacterTacticalActionDefinition fallback = null;
            for (int i = 0; i < loadout.ActionIds.Count; i++)
            {
                CharacterTacticalActionDefinition candidate = _player.Catalog.Action(loadout.ActionIds[i]);
                if (candidate == null || !candidate.CharacterStableIds.Contains(characterStableId)) continue;
                fallback = candidate; break;
            }
            if (fallback == null) return false;
            if (binding == null)
            {
                loadout.Bindings.Add(new CharacterTacticalSlotBinding
                {
                    CardInstanceId = reference.InstanceId,
                    CardId = reference.CardId,
                    SlotPath = card.ActionSlotPath,
                    TacticalActionId = fallback.Id
                });
            }
            else
            {
                binding.CardId = reference.CardId;
                binding.SlotPath = card.ActionSlotPath;
                binding.TacticalActionId = fallback.Id;
            }
            return true;
        }

        private void Validate()
        {
            if ((_state?.Schema != TacticalBattlePreparationState.CurrentSchema
                    && _state?.Schema != TacticalBattlePreparationState.CharacterItemSchema)
                || _state.Working?.Entities == null || _state.Presets == null || _state.NextEntityId == 0)
                throw new JsonSerializationException("TACTICAL_BATTLE_PREPARATION_INVALID");
            ValidateSetup(_state.Working);
            var presets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var preset in _state.Presets)
            {
                if (preset == null || string.IsNullOrWhiteSpace(preset.PresetId) || !presets.Add(preset.PresetId))
                    throw new JsonSerializationException("TACTICAL_BATTLE_PRESET_INVALID");
                ValidateSetup(preset.Snapshot);
            }
        }

        private void ValidateSetup(TacticalBattleSetup setup)
        {
            if (setup?.Entities == null) throw new JsonSerializationException("TACTICAL_BATTLE_SETUP_INVALID");
            var ids = new HashSet<string>(StringComparer.Ordinal); var selected = new HashSet<int>();
            foreach (var entity in setup.Entities)
            {
                if (entity == null || string.IsNullOrWhiteSpace(entity.EntityId) || !ids.Add(entity.EntityId)
                    || entity.CharacterId <= 0 || string.IsNullOrWhiteSpace(entity.CharacterStableId)
                    || entity.TeamId <= 0 || entity.Loadout == null || entity.NextCardInstanceId == 0)
                    throw new JsonSerializationException("TACTICAL_BATTLE_ENTITY_INVALID");
                if (entity.CharacterItemInstanceId.HasValue || entity.InventoryOwnerId.HasValue)
                {
                    if (_state.Schema != TacticalBattlePreparationState.CharacterItemSchema
                        || entity.CharacterItemInstanceId.GetValueOrDefault() <= 0 || entity.InventoryOwnerId.GetValueOrDefault() <= 0
                        || !selected.Add(entity.CharacterItemInstanceId.Value)
                        || entity.Loadout.CharacterStableId != entity.CharacterStableId || entity.Loadout.Cards?.Count != 0
                        || entity.Loadout.Bindings?.Count != 0 || entity.Loadout.ActionIds?.Count != 0)
                        throw new JsonSerializationException("TACTICAL_BATTLE_CHARACTER_ITEM_REFERENCE_INVALID");
                }
            }
            if (!string.IsNullOrEmpty(setup.PlayerControlledEntityId) && !ids.Contains(setup.PlayerControlledEntityId))
                throw new JsonSerializationException("TACTICAL_BATTLE_PLAYER_ENTITY_INVALID");
        }

        private void Touch()
        {
            if (_initializing) return;
            Revision++; Changed?.Invoke(); _ = FlushAsync();
        }
        private static float NormalizeYaw(float yaw) => Mathf.Repeat(yaw, 360f);
        private static T Clone<T>(T value) => value == null ? default : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        private static bool Fail(string message, out string issue) { issue = message; return false; }
    }
}
