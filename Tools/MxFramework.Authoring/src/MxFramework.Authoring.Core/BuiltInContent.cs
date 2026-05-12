using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class BuiltInContent
    {
        public static IReadOnlyList<AuthoringWorkflow> CreateBuiltInWorkflows()
        {
            return new[] { CreateBuffWorkflow() };
        }

        public static AuthoringWorkflow GetWorkflow(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId)) return null;
            IReadOnlyList<AuthoringWorkflow> all = CreateBuiltInWorkflows();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].WorkflowId == workflowId) return all[i];
            }
            return null;
        }

        public static ProjectAuthoringManifest CreateProjectManifest()
        {
            var manifest = new ProjectAuthoringManifest
            {
                ProjectId = "wgameframework.demo",
                DisplayName = "WGameFramework Demo Authoring Package",
                AuthoringVersion = "0.1.0",
                SchemaVersion = "1.0"
            };
            manifest.Schemas.Add(CreateBuffSchema());
            manifest.Enums.Add(CreateBuffTypeEnum());
            manifest.Enums.Add(CreateBuffAddTypeEnum());
            manifest.Enums.Add(CreateBuffTargetEnum());
            manifest.Enums.Add(CreateAttrTypeEnum());
            manifest.Enums.Add(CreateDamageTypeEnum());
            manifest.Enums.Add(CreateElementTypeEnum());
            manifest.Enums.Add(CreateStatusTypeEnum());
            manifest.Enums.Add(CreateConditionTypeEnum());
            manifest.Enums.Add(CreatePositionTypeEnum());
            manifest.References.Add(CreateBuffReferenceIndex());
            manifest.Workflows.Add(CreateBuffWorkflow());
            manifest.Localization.Add(new LocalizationEntry
            {
                Key = "buff.sample.fire.name",
                ZhCN = "示例燃烧",
                EnUS = "Sample Burn"
            });
            manifest.Localization.Add(new LocalizationEntry
            {
                Key = "buff.sample.fire.desc",
                ZhCN = "每秒造成一次火焰伤害。",
                EnUS = "Deals fire damage once per second."
            });
            manifest.AssetWhitelistPrefixes.Add("Effects/");
            manifest.AssetWhitelistPrefixes.Add("Audio/");
            manifest.AssetWhitelistPrefixes.Add("UI/");
            return manifest;
        }

        public static AuthoringWorkflow CreateBuffWorkflow()
        {
            var workflow = new AuthoringWorkflow
            {
                WorkflowId = "buff.create",
                Title = "创建 Buff",
                Category = "Buff",
                Status = WorkflowStatus.InProgress,
                Mode = WorkflowMode.Mod,
                CurrentStepId = "buff-type",
                Target = new WorkflowTarget
                {
                    Source = "BuffFactoryData",
                    Layer = "Mod"
                }
            };

            workflow.Steps.Add(CreateStep("intent", "确定 Buff 设计目标", "描述玩法目的并推荐 BuffType.", WorkflowStatus.Ready, "Design intent", "Buff purpose"));
            workflow.Steps.Add(CreateStep("buff-type", "选择 Buff 类型", "选择 WGame 风格 BuffType.", WorkflowStatus.Ready, "BuffFactoryData.Type", "BuffType"));
            workflow.Steps.Add(CreateStep("common-fields", "填写公共字段", "填写 ID、Name、Desc、ShowHeadIcon 和 Removeable.", WorkflowStatus.NotStarted, "BuffData.ID", "BuffData common fields"));
            workflow.Steps.Add(CreateStep("stacking-lifetime", "配置目标、堆叠和持续", "填写 Target、AddType、AddNum、Duration 和 HitCooldown.", WorkflowStatus.NotStarted, "BuffData.Duration", "Buff lifetime policy"));
            workflow.Steps.Add(CreateStep("type-fields", "填写类型专属字段", "根据 BuffType 填写专属字段.", WorkflowStatus.Blocked, "BuffData subtype payload", "BuffData subtype payload"));
            workflow.Steps.Add(CreateStep("validation", "运行校验", "校验字段、引用、多语言和 Patch 冲突.", WorkflowStatus.NotStarted, "ModPackage", "ValidationReport"));
            workflow.Steps.Add(CreateStep("merged-preview", "预览合并结果", "预览运行时最终读取的 Buff Patch.", WorkflowStatus.NotStarted, "PatchDocument", "MergePreview"));
            workflow.Steps.Add(CreateStep("export", "导出报告 / Mod Patch", "导出报告和 Mod Patch.", WorkflowStatus.NotStarted, "MergePreview", "ModPackage"));
            return workflow;
        }

        public static ConfigSchema CreateBuffSchema()
        {
            var schema = new ConfigSchema
            {
                SchemaId = "BuffFactoryData",
                DisplayName = "Buff 配置",
                StructureKind = "Table"
            };
            AddField(schema, "Id", "编号", FieldType.Integer, true, "common", "公共字段", description: "Buff 稳定 ID。创建后尽量不要修改，供引用、存档和 Mod Patch 使用。");
            AddField(schema, "Type", "Buff 类型", FieldType.Enum, true, "common", "公共字段", enumId: "wgame.BuffType", description: "决定下方会出现哪些类型专属字段。");
            AddField(schema, "Name", "名称", FieldType.LocalizedText, true, "common", "公共字段", description: "多语言 key 或直接文本。");
            AddField(schema, "Desc", "描述", FieldType.LocalizedText, false, "common", "公共字段", description: "多语言 key 或直接文本。");
            AddField(schema, "ShowHeadIcon", "显示头顶图标", FieldType.Boolean, false, "common", "公共字段");
            AddField(schema, "Removeable", "可移除", FieldType.Boolean, false, "common", "公共字段", description: "玩家或系统是否允许移除。");

            AddField(schema, "Target", "生效目标", FieldType.Enum, true, "stacking", "目标 / 堆叠 / 持续", enumId: "wgame.BuffTargetType", description: "Buff 添加到谁身上。DamageByAttr 通常选择目标。");
            AddField(schema, "AddType", "重复添加规则", FieldType.Enum, true, "stacking", "目标 / 堆叠 / 持续", enumId: "wgame.BuffAddType", description: "同一个 Buff 重复添加时刷新、替换或叠层的规则。");
            AddField(schema, "AddNum", "叠层数量", FieldType.Integer, false, "stacking", "目标 / 堆叠 / 持续", description: "每次添加增加的层数；为空时由运行时默认规则处理。");
            AddField(schema, "Duration", "持续时间", FieldType.Integer, true, "stacking", "目标 / 堆叠 / 持续", unit: "ms", description: "Buff 存在时长，单位毫秒。DamageByAttr 必须大于 0。");
            AddField(schema, "HitCooldown", "触发间隔", FieldType.Integer, false, "stacking", "目标 / 堆叠 / 持续", unit: "ms", description: "周期伤害或周期触发间隔。DamageByAttr 必须大于 0，且不建议超过持续时间。");

            AddField(schema, "AttrID", "属性", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.AttrType", visibleWhen: new[] { "Numerical", "ChangeAttr" });
            AddField(schema, "AddValue", "加值", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "Numerical", "ChangeAttr" });
            AddField(schema, "MulValue", "倍率", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "Numerical", "ChangeAttr" });
            AddField(schema, "IsBuff", "是否增益", FieldType.Boolean, false, "type", "类型专属字段", visibleWhen: new[] { "Numerical" });
            AddField(schema, "IsNeutralBuff", "中立 Buff", FieldType.Boolean, false, "type", "类型专属字段", visibleWhen: new[] { "Numerical" });

            AddField(schema, "ConditionType", "条件类型", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.ConditionType", visibleWhen: new[] { "Condition" });
            AddField(schema, "AddBuffList", "满足后添加 Buff", FieldType.Reference, false, "type", "类型专属字段", referenceSource: "BuffFactoryData", visibleWhen: new[] { "Condition" }, description: "逗号分隔 Buff ID。", isList: true);
            AddField(schema, "RemoveBuffList", "满足后移除 Buff", FieldType.Reference, false, "type", "类型专属字段", referenceSource: "BuffFactoryData", visibleWhen: new[] { "Condition" }, description: "逗号分隔 Buff ID。", isList: true);

            AddField(schema, "Values", "伤害公式", FieldType.String, true, "type", "类型专属字段", visibleWhen: new[] { "DamageByAttr" }, description: "当前预览支持固定数值，或 caster.Attack * 系数，例如 caster.Attack * 0.35。");
            AddField(schema, "DmgType", "伤害类型", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.DamageType", visibleWhen: new[] { "DamageByAttr", "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "EleType", "元素类型", FieldType.Enum, false, "type", "类型专属字段", enumId: "wgame.ElementType", visibleWhen: new[] { "DamageByAttr", "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "EleValue", "元素值", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "DamageByAttr", "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "DamageBaseTypeID", "伤害基准类型", FieldType.Integer, false, "type", "类型专属字段", visibleWhen: new[] { "DamageByAttr" }, description: "开发模式字段；Mod 模式默认隐藏，避免玩家误改底层基准。");

            AddField(schema, "HitTarget", "命中目标", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.BuffTargetType", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "HitBuffs", "命中 Buff", FieldType.Reference, false, "type", "类型专属字段", referenceSource: "BuffFactoryData", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" }, description: "逗号分隔 Buff ID。", isList: true);
            AddField(schema, "HitSkill", "命中技能", FieldType.Reference, false, "type", "类型专属字段", referenceSource: "SkillData", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "StartPosition", "起点位置", FieldType.Enum, false, "type", "类型专属字段", enumId: "wgame.PositionType", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "EndPosition", "终点位置", FieldType.Enum, false, "type", "类型专属字段", enumId: "wgame.PositionType", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "Speed", "速度", FieldType.Float, true, "type", "类型专属字段", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "Accelerate", "加速度", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbBezier", "CastOrbLinear" });
            AddField(schema, "DelayTime", "延迟时间", FieldType.Integer, false, "type", "类型专属字段", unit: "ms", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "OffsetRadius", "偏移半径", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack" });
            AddField(schema, "OffsetType", "偏移类型", FieldType.String, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack" });
            AddField(schema, "MoveAcc", "移动加速度", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbTrack" });
            AddField(schema, "Gravity", "重力", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbTrack" });
            AddField(schema, "RotSpeed", "旋转速度", FieldType.Float, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbTrack" });
            AddField(schema, "UseParabola", "使用抛物线", FieldType.Boolean, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbTrack" });
            AddField(schema, "EulerAngle", "直线角度", FieldType.String, false, "type", "类型专属字段", visibleWhen: new[] { "CastOrbLinear" });

            AddField(schema, "PAttrType", "被动属性", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.AttrType", visibleWhen: new[] { "Positive" });
            AddField(schema, "PValue", "被动属性值", FieldType.Float, true, "type", "类型专属字段", visibleWhen: new[] { "Positive" });
            AddField(schema, "StatusType", "状态类型", FieldType.Enum, true, "type", "类型专属字段", enumId: "wgame.StatusBuffType", visibleWhen: new[] { "Status" }, description: "属性影响由状态类型推导。");

            AddField(schema, "StartEffect", "开始特效", FieldType.AssetPath, false, "presentation", "表现资源");
            AddField(schema, "LoopEffect", "持续特效", FieldType.AssetPath, false, "presentation", "表现资源", visibleWhen: new[] { "Numerical", "ChangeAttr", "Positive", "Status" });
            AddField(schema, "HitEffect", "命中特效", FieldType.AssetPath, false, "presentation", "表现资源", visibleWhen: new[] { "DamageByAttr", "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            AddField(schema, "FlyEffect", "飞行特效", FieldType.AssetPath, false, "presentation", "表现资源", visibleWhen: new[] { "CastOrbBezier", "CastOrbTrack", "CastOrbLinear" });
            return schema;
        }

        public static EnumDomain CreateBuffTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.BuffType" };
            AddOption(domain, "Numerical", "属性数值", 1);
            AddOption(domain, "Condition", "条件触发", 2);
            AddOption(domain, "ChangeAttr", "改变属性", 3);
            AddOption(domain, "DamageByAttr", "持续伤害", 4);
            AddOption(domain, "CastOrbBezier", "贝塞尔弹道", 5);
            AddOption(domain, "CastOrbTrack", "追踪弹道", 6);
            AddOption(domain, "CastOrbLinear", "直线弹道", 7);
            AddOption(domain, "Positive", "被动属性", 8);
            AddOption(domain, "Status", "状态控制", 9);
            return domain;
        }

        public static EnumDomain CreateBuffAddTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.BuffAddType" };
            AddOption(domain, "AddNone", "不叠加", 0);
            AddOption(domain, "RefreshAllTime", "刷新持续时间", 1);
            AddOption(domain, "ReplaceMaxTime", "替换为最大持续时间", 2);
            AddOption(domain, "RefreshAllTimeAndAdd", "刷新并增加层数", 3);
            AddOption(domain, "ReplaceFist", "替换最早添加", 4);
            AddOption(domain, "UseSingleTime", "使用单次时间", 5);
            return domain;
        }

        public static EnumDomain CreateBuffTargetEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.BuffTargetType" };
            AddOption(domain, "Self", "自身", 0);
            AddOption(domain, "Caster", "施加者", 1);
            AddOption(domain, "Target", "目标", 2);
            AddOption(domain, "Owner", "拥有者", 3);
            return domain;
        }

        public static EnumDomain CreateAttrTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.AttrType" };
            AddOption(domain, "Attack", "攻击", 1);
            AddOption(domain, "Defense", "防御", 2);
            AddOption(domain, "Hp", "生命", 3);
            AddOption(domain, "MoveSpeed", "移动速度", 4);
            AddOption(domain, "Crit", "暴击", 5);
            return domain;
        }

        public static EnumDomain CreateDamageTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.DamageType" };
            AddOption(domain, "Normal", "普通", 0);
            AddOption(domain, "Physical", "物理", 1);
            AddOption(domain, "Magic", "法术", 2);
            AddOption(domain, "TrueDamage", "真实伤害", 3);
            return domain;
        }

        public static EnumDomain CreateElementTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.ElementType" };
            AddOption(domain, "None", "无", 0);
            AddOption(domain, "Fire", "火", 1);
            AddOption(domain, "Ice", "冰", 2);
            AddOption(domain, "Lightning", "雷", 3);
            return domain;
        }

        public static EnumDomain CreateStatusTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.StatusBuffType" };
            AddOption(domain, "Stun", "眩晕", 0);
            AddOption(domain, "Paralysis", "麻痹", 2);
            AddOption(domain, "SuperArmor", "霸体", 3);
            AddOption(domain, "Focus", "专注", 14);
            AddOption(domain, "FireEnchant", "火焰附魔", 15);
            return domain;
        }

        public static EnumDomain CreateConditionTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.ConditionType" };
            AddOption(domain, "CheckHP", "检查生命", 1);
            AddOption(domain, "OnBeHit", "受击时", 2);
            AddOption(domain, "CheckStatus", "检查状态", 3);
            return domain;
        }

        public static EnumDomain CreatePositionTypeEnum()
        {
            var domain = new EnumDomain { EnumId = "wgame.PositionType" };
            AddOption(domain, "Self", "自身位置", 0);
            AddOption(domain, "Target", "目标位置", 1);
            AddOption(domain, "Socket", "挂点", 2);
            AddOption(domain, "WorldPoint", "世界点", 3);
            return domain;
        }

        public static ReferenceIndex CreateBuffReferenceIndex()
        {
            var index = new ReferenceIndex { Source = "BuffFactoryData" };
            index.Entries.Add(new ReferenceEntry
            {
                Source = "BuffFactoryData",
                Id = "100001",
                DisplayName = "示例燃烧",
                Kind = "Buff"
            });
            return index;
        }

        private static WorkflowStep CreateStep(string id, string title, string description, WorkflowStatus status, string input, string output)
        {
            var step = new WorkflowStep
            {
                StepId = id,
                Title = title,
                Description = description,
                Status = status,
                Actor = WorkflowActor.Human,
                AiPromptHint = "只围绕当前步骤给出可执行建议。"
            };
            step.Inputs.Add(input);
            step.Outputs.Add(output);
            step.Checks.Add("No blocking errors");
            return step;
        }

        private static void AddOption(EnumDomain domain, string name, string displayName, int value)
        {
            domain.Options.Add(new EnumOption
            {
                Name = name,
                DisplayName = displayName,
                Value = value
            });
        }

        private static void AddField(ConfigSchema schema, string name, string displayName, FieldType type, bool required, string groupId, string groupDisplayName, string enumId = "", string referenceSource = "", string unit = "", string description = "", string[] visibleWhen = null, bool isList = false)
        {
            var field = new SchemaField
            {
                Name = name,
                DisplayName = displayName,
                Type = type,
                Required = required,
                EnumId = enumId,
                ReferenceSource = referenceSource,
                Unit = unit,
                Description = description,
                GroupId = groupId,
                GroupDisplayName = groupDisplayName,
                IsList = isList
            };

            if (visibleWhen != null)
                field.VisibleWhenBuffTypes.AddRange(visibleWhen);
            schema.Fields.Add(field);
        }
    }
}
