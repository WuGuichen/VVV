using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public enum BuffAuthoringTemplateKind
    {
        Numerical,
        Condition,
        ChangeAttr,
        DamageByAttr,
        CastOrbBezier,
        CastOrbTrack,
        CastOrbLinear,
        Positive,
        Status
    }

    public static class BuffAuthoringWorkflowTemplate
    {
        public static AuthoringWorkflow CreateCreateBuffWorkflow(
            string workflowId,
            string title,
            int buffId,
            AuthoringWorkflowMode mode = AuthoringWorkflowMode.Player,
            string layer = "Mod",
            BuffAuthoringTemplateKind templateKind = BuffAuthoringTemplateKind.Numerical)
        {
            return new AuthoringWorkflow(
                    workflowId,
                    title,
                    "Buff",
                    AuthoringWorkflowStatus.InProgress,
                    mode,
                    new AuthoringWorkflowTarget("BuffFactoryData", buffId, layer: layer),
                    "buff-type")
                .AddStep(CreateIntentStep())
                .AddStep(CreateBuffTypeStep(templateKind))
                .AddStep(CreateCommonFieldsStep())
                .AddStep(CreateStackingAndLifetimeStep())
                .AddStep(CreateTypeFieldsStep(templateKind))
                .AddStep(CreatePresentationStep(templateKind))
                .AddStep(CreateReferenceStep(templateKind))
                .AddStep(CreateLocalizationStep())
                .AddStep(CreateLayerStep())
                .AddStep(CreateValidationStep())
                .AddStep(CreatePreviewStep())
                .AddStep(CreateExportStep());
        }

        private static AuthoringWorkflowStep CreateIntentStep()
        {
            return new AuthoringWorkflowStep(
                    "intent",
                    "确定 Buff 设计目标",
                    "先描述 Buff 要解决的玩法问题，再决定是否需要状态、属性、伤害、条件或弹道逻辑。",
                    AuthoringWorkflowStatus.Ready,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "根据玩法目标判断 Buff 倾向于哪一种 WGame BuffType，只输出推荐类型和理由。")
                .AddInput("Design intent")
                .AddOutput("Buff purpose")
                .AddCheck("Purpose is clear")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenDocument, "查看 WGame Buff 流程规范", document: "Docs/WGAME_BUFF_AUTHORING_WORKFLOW.md"));
        }

        private static AuthoringWorkflowStep CreateBuffTypeStep(BuffAuthoringTemplateKind templateKind)
        {
            return new AuthoringWorkflowStep(
                    "buff-type",
                    "选择 Buff 类型",
                    "选择 WGame 风格的 BuffType。类型决定后续专属字段、校验规则和运行时 Status。",
                    AuthoringWorkflowStatus.Ready,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "检查当前玩法目标和已选 BuffType 是否匹配，不要改写其他字段。")
                .AddInput("BuffType")
                .AddOutput("BuffFactoryData.Type = " + templateKind)
                .AddCheck("BuffType is not None")
                .AddCheck("Buff data subtype matches BuffType")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看 Type 字段", source: "BuffFactoryData", field: "Type"));
        }

        private static AuthoringWorkflowStep CreateCommonFieldsStep()
        {
            return new AuthoringWorkflowStep(
                    "common-fields",
                    "填写公共字段",
                    "填写 BuffData 公共字段：ID、Name、Desc、ShowHeadIcon 和 Removeable。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "只检查公共字段是否缺失、命名是否清晰、描述是否能解释玩法效果。")
                .AddInput("BuffData.ID")
                .AddInput("BuffData.Name")
                .AddInput("BuffData.Desc")
                .AddInput("BuffData.ShowHeadIcon")
                .AddInput("BuffData.Removeable")
                .AddOutput("BuffData common fields")
                .AddCheck("Id > 0")
                .AddCheck("Name is readable")
                .AddCheck("Desc is readable")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenConfigSource, "打开 Buff 配置源", source: "BuffFactoryData"))
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看 ID 字段", source: "BuffData", field: "ID"));
        }

        private static AuthoringWorkflowStep CreateStackingAndLifetimeStep()
        {
            return new AuthoringWorkflowStep(
                    "stacking-lifetime",
                    "配置目标、堆叠和持续",
                    "配置 BuffData.Target、AddType、AddNum、Duration 和 HitCooldown，这决定 Buff 如何添加、刷新、替换和周期触发。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "检查 AddType、AddNum、Duration、HitCooldown 组合是否符合目标效果。")
                .AddInput("BuffData.Target")
                .AddInput("BuffData.AddType")
                .AddInput("BuffData.AddNum")
                .AddInput("BuffData.Duration")
                .AddInput("BuffData.HitCooldown")
                .AddOutput("Buff lifetime policy")
                .AddCheck("AddNum > 0")
                .AddCheck("Duration is valid")
                .AddCheck("HitCooldown is valid for periodic effects")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看堆叠类型", source: "BuffData", field: "AddType"))
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看持续时间", source: "BuffData", field: "Duration"));
        }

        private static AuthoringWorkflowStep CreateTypeFieldsStep(BuffAuthoringTemplateKind templateKind)
        {
            return new AuthoringWorkflowStep(
                    "type-fields",
                    "填写类型专属字段",
                    CreateTypeDescription(templateKind),
                    AuthoringWorkflowStatus.Blocked,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "只围绕当前 BuffType 的专属字段检查缺失、冲突和单位问题。")
                .AddInput(CreateTypeInput(templateKind, 0))
                .AddInput(CreateTypeInput(templateKind, 1))
                .AddInput(CreateTypeInput(templateKind, 2))
                .AddOutput(CreateTypeOutput(templateKind))
                .AddCheck(CreateTypeCheck(templateKind))
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看类型专属字段", source: CreateTypeSource(templateKind)));
        }

        private static AuthoringWorkflowStep CreatePresentationStep(BuffAuthoringTemplateKind templateKind)
        {
            return new AuthoringWorkflowStep(
                    "presentation",
                    "配置表现资源",
                    "配置图标展示、开始特效、持续特效、命中特效、部位、缩放和时长。没有表现需求的类型应明确留空。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "检查表现资源是否和当前 BuffType 匹配，指出缺失资源或不需要资源的字段。")
                .AddInput("BuffData.ShowHeadIcon")
                .AddInput(CreatePresentationInput(templateKind))
                .AddOutput("Presentation settings")
                .AddCheck("Asset paths are valid or intentionally empty")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenField, "查看表现字段", source: CreateTypeSource(templateKind)));
        }

        private static AuthoringWorkflowStep CreateReferenceStep(BuffAuthoringTemplateKind templateKind)
        {
            return new AuthoringWorkflowStep(
                    "references",
                    "检查引用关系",
                    "检查 Buff 引用的属性、状态、条件、技能、特效、元素、Buff 列表等目标是否存在。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Tool,
                    aiPromptHint: "按引用类型列出缺失目标，只给出可定位修复建议。")
                .AddInput(CreateReferenceInput(templateKind))
                .AddOutput("Reference validation result")
                .AddCheck("All referenced ids exist")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.OpenReferenceTarget, "打开引用目标", source: CreateTypeSource(templateKind)));
        }

        private static AuthoringWorkflowStep CreateLocalizationStep()
        {
            return new AuthoringWorkflowStep(
                    "localization",
                    "填写多语言",
                    "从 BuffData.Name 和 BuffData.Desc 生成或校验中文、英文文本。WGame 旧数据可能直接保存文本，新流程仍要为多语言预留独立层。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.AI,
                    aiPromptHint: "根据 Buff 目标生成中文和英文名称、描述建议。")
                .AddInput("BuffData.Name")
                .AddInput("BuffData.Desc")
                .AddOutput("Localization patch")
                .AddCheck("ZhCN text exists")
                .AddCheck("EnUS text exists");
        }

        private static AuthoringWorkflowStep CreateLayerStep()
        {
            return new AuthoringWorkflowStep(
                    "layer",
                    "选择 Patch / Mod 层",
                    "选择变更保存到 Patch、Mod 或 Debug 层；玩家模式只能写 Mod 层。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Human,
                    aiPromptHint: "确认当前模式是否允许写入目标层。")
                .AddInput("Target layer")
                .AddOutput("ConfigPatchEntry<BuffFactoryData>")
                .AddCheck("Player Mode writes Mod only");
        }

        private static AuthoringWorkflowStep CreateValidationStep()
        {
            return new AuthoringWorkflowStep(
                    "validation",
                    "运行校验",
                    "校验 BuffType、公共字段、类型专属字段、引用、多语言和 Patch/Mod 冲突。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Tool,
                    aiPromptHint: "解释校验报告中的阻塞项，并按字段列出修复步骤。")
                .AddInput("BuffFactoryData")
                .AddInput("BuffData")
                .AddOutput("ConfigTableValidationReport")
                .AddCheck("No Error")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.RunValidation, "运行 Buff 校验", source: "BuffFactoryData"));
        }

        private static AuthoringWorkflowStep CreatePreviewStep()
        {
            return new AuthoringWorkflowStep(
                    "merged-preview",
                    "预览合并结果",
                    "预览 Base + Patch + Mod 后的运行时 BuffFactoryData、BuffData 子类和 ChangeSet。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Tool,
                    aiPromptHint: "总结 ChangeSet 中新增、替换、删除的 Buff 行。")
                .AddInput("ConfigPatchEntry<BuffFactoryData>")
                .AddOutput("Merged ConfigTable")
                .AddOutput("ConfigChangeSet")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.PreviewMergedResult, "预览合并结果", source: "BuffFactoryData"));
        }

        private static AuthoringWorkflowStep CreateExportStep()
        {
            return new AuthoringWorkflowStep(
                    "export",
                    "导出报告 / Mod Patch",
                    "导出可复查报告和 Mod Patch；失败时不影响 Base 配置。",
                    AuthoringWorkflowStatus.NotStarted,
                    AuthoringWorkflowActor.Tool,
                    aiPromptHint: "检查导出报告是否足以让人类复查。")
                .AddInput("Merged preview")
                .AddOutput("Mod Patch")
                .AddOutput("Authoring report")
                .AddCheck("Report exists")
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.ExportReport, "导出报告"))
                .AddAction(new AuthoringQuickAction(AuthoringQuickActionKind.ExportModPatch, "导出 Mod Patch"));
        }

        private static string CreateTypeSource(BuffAuthoringTemplateKind templateKind)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.Condition:
                    return "CBuffData";
                case BuffAuthoringTemplateKind.ChangeAttr:
                    return "SBuffChangeAttrData";
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return "SBuffDamageByAttrData";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                    return "SBuffCastOrbBezierData";
                case BuffAuthoringTemplateKind.CastOrbTrack:
                    return "SBuffCastOrbTrackData";
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return "SBuffCastOrbLinearData";
                case BuffAuthoringTemplateKind.Positive:
                    return "PBuffData";
                case BuffAuthoringTemplateKind.Status:
                    return "StatusBuffData";
                default:
                    return "NBuffData";
            }
        }

        private static string CreateTypeDescription(BuffAuthoringTemplateKind templateKind)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.Condition:
                    return "配置条件列表，以及满足条件后添加或移除的 Buff 列表。";
                case BuffAuthoringTemplateKind.ChangeAttr:
                    return "配置属性类型、加值、乘值和持续属性变化表现。";
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return "配置按属性计算的伤害值、伤害类型、元素、削韧和命中特效。";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                    return "配置贝塞尔弹道的起点、目标、命中、技能、特效和元素。";
                case BuffAuthoringTemplateKind.CastOrbTrack:
                    return "配置追踪弹道的起点、目标、命中、技能、特效和元素。";
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return "配置直线弹道的起点、目标、命中、技能、特效和元素。";
                case BuffAuthoringTemplateKind.Positive:
                    return "配置被动属性类型和千分比属性值。";
                case BuffAuthoringTemplateKind.Status:
                    return "配置状态类型，并由状态类型推导属性影响和是否增益。";
                default:
                    return "配置属性类型、加值、乘值、增益/中立标记和开始特效。";
            }
        }

        private static string CreateTypeInput(BuffAuthoringTemplateKind templateKind, int index)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.Condition:
                    return index == 0 ? "CBuffData.AddBuffList" : index == 1 ? "CBuffData.RemoveBuffList" : "CBuffData.ConditionList";
                case BuffAuthoringTemplateKind.ChangeAttr:
                    return index == 0 ? "SBuffChangeAttrData.AttrID" : index == 1 ? "SBuffChangeAttrData.AddValue" : "SBuffChangeAttrData.MulValue";
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return index == 0 ? "SBuffDamageByAttrData.Values" : index == 1 ? "SBuffDamageByAttrData.DmgType" : "SBuffDamageByAttrData.EleType";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                case BuffAuthoringTemplateKind.CastOrbTrack:
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return index == 0 ? CreateTypeSource(templateKind) + ".HitSkill" : index == 1 ? CreateTypeSource(templateKind) + ".HitBuffs" : CreateTypeSource(templateKind) + ".HitTarget";
                case BuffAuthoringTemplateKind.Positive:
                    return index == 0 ? "PBuffData.PAttrType" : index == 1 ? "PBuffData.PValue" : "BuffData.Duration";
                case BuffAuthoringTemplateKind.Status:
                    return index == 0 ? "StatusBuffData.StatusType" : index == 1 ? "StatusBuffData.AttrType" : "StatusBuffData.IsBuff";
                default:
                    return index == 0 ? "NBuffData.AttrID" : index == 1 ? "NBuffData.AddValue" : "NBuffData.MulValue";
            }
        }

        private static string CreateTypeOutput(BuffAuthoringTemplateKind templateKind)
        {
            return CreateTypeSource(templateKind) + " payload";
        }

        private static string CreateTypeCheck(BuffAuthoringTemplateKind templateKind)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.Condition:
                    return "At least one condition or buff list entry exists";
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return "Damage value and damage type are valid";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                case BuffAuthoringTemplateKind.CastOrbTrack:
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return "Hit target, hit skill and projectile settings are valid";
                case BuffAuthoringTemplateKind.Status:
                    return "StatusType can derive status info";
                default:
                    return "Type-specific numeric fields are valid";
            }
        }

        private static string CreatePresentationInput(BuffAuthoringTemplateKind templateKind)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return "SBuffDamageByAttrData.Eff";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                case BuffAuthoringTemplateKind.CastOrbTrack:
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return CreateTypeSource(templateKind) + ".Effect";
                case BuffAuthoringTemplateKind.Positive:
                case BuffAuthoringTemplateKind.Status:
                    return "BuffData.ShowHeadIcon";
                default:
                    return CreateTypeSource(templateKind) + ".EffectStart";
            }
        }

        private static string CreateReferenceInput(BuffAuthoringTemplateKind templateKind)
        {
            switch (templateKind)
            {
                case BuffAuthoringTemplateKind.Condition:
                    return "Buff ids and ConditionType ids";
                case BuffAuthoringTemplateKind.DamageByAttr:
                    return "Attribute, damage type and element ids";
                case BuffAuthoringTemplateKind.CastOrbBezier:
                case BuffAuthoringTemplateKind.CastOrbTrack:
                case BuffAuthoringTemplateKind.CastOrbLinear:
                    return "Skill, buff, target, audio, element and effect ids";
                case BuffAuthoringTemplateKind.Status:
                    return "StatusBuffType id";
                default:
                    return "Attribute and effect ids";
            }
        }
    }
}
