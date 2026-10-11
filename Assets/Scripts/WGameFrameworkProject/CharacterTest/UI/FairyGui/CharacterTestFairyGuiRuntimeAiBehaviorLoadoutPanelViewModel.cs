using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload
    {
        public int EntityId { get; set; }
        public int LoadoutId { get; set; }
        public int VariantId { get; set; }
    }

    public sealed class CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel
    {
        public int Index { get; set; }
        public int EntityId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public bool Selected { get; set; }
    }

    public sealed class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel
    {
        public int Index { get; set; }
        public int LoadoutId { get; set; }
        public int VariantId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public bool Selected { get; set; }
        public bool ActiveOnSelectedEntity { get; set; }
    }

    public sealed class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel
    {
        private CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel[] _entities =
            Array.Empty<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel>();
        private CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel[] _loadouts =
            Array.Empty<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel>();

        public string Title { get; set; } = "Runtime AI Planner Strategies";
        public string StatusText { get; set; } = "Ready";
        public string Message { get; set; } = "Choose an AI unit and a published strategy.";
        public string PhaseText { get; set; } = "Selection locked";
        public bool PhaseUnlocked { get; set; }
        public int SelectedEntityId { get; set; }
        public int SelectedLoadoutId { get; set; }
        public int SelectedVariantId { get; set; }
        public string StrategyName { get; set; } = "No strategy selected";
        public string StrategyDescription { get; set; } = string.Empty;
        public string PrimaryActionSummary { get; set; } = string.Empty;
        public string SuitableScenario { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public string BehaviorOrderText { get; set; } = string.Empty;
        public string ActionChainText { get; set; } = string.Empty;
        public string WarningText { get; set; } = string.Empty;
        public string ApplyLabel { get; set; } = "Apply";
        public bool CanApply { get; set; }
        public int TotalEntityCount { get; set; }
        public int TotalLoadoutCount { get; set; }
        public IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> Entities => _entities;
        public IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> Loadouts => _loadouts;

        public void SetEntities(
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                _entities = Array.Empty<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel>();
                return;
            }

            _entities = new CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                _entities[i] = entities[i];
        }

        public void SetLoadouts(
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> loadouts)
        {
            if (loadouts == null || loadouts.Count == 0)
            {
                _loadouts = Array.Empty<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel>();
                return;
            }

            _loadouts = new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel[loadouts.Count];
            for (int i = 0; i < loadouts.Count; i++)
                _loadouts[i] = loadouts[i];
        }
    }

    internal static class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(512);
            builder.Append(viewModel.PhaseUnlocked ? '1' : '0').Append('|')
                .Append(viewModel.CanApply ? '1' : '0').Append('|')
                .Append(viewModel.SelectedEntityId).Append('|')
                .Append(viewModel.SelectedLoadoutId).Append('|')
                .Append(viewModel.SelectedVariantId).Append('|')
                .Append(viewModel.TotalEntityCount).Append('|')
                .Append(viewModel.TotalLoadoutCount).Append('|');
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.Message);
            AppendToken(builder, viewModel.PhaseText);
            AppendToken(builder, viewModel.StrategyName);
            AppendToken(builder, viewModel.StrategyDescription);
            AppendToken(builder, viewModel.PrimaryActionSummary);
            AppendToken(builder, viewModel.SuitableScenario);
            AppendToken(builder, viewModel.RiskText);
            AppendToken(builder, viewModel.TagsText);
            AppendToken(builder, viewModel.BehaviorOrderText);
            AppendToken(builder, viewModel.ActionChainText);
            AppendToken(builder, viewModel.WarningText);
            AppendToken(builder, viewModel.ApplyLabel);
            AppendEntities(builder, viewModel.Entities);
            AppendLoadouts(builder, viewModel.Loadouts);
            return builder.ToString();
        }

        private static void AppendEntities(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> entities)
        {
            builder.Append("entities=").Append(entities != null ? entities.Count : 0).Append('|');
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel item = entities[i];
                builder.Append(item.Index).Append('|')
                    .Append(item.EntityId).Append('|')
                    .Append(item.Visible ? '1' : '0').Append('|')
                    .Append(item.Selected ? '1' : '0').Append('|');
                AppendToken(builder, item.Label);
                AppendToken(builder, item.DetailText);
            }
        }

        private static void AppendLoadouts(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> loadouts)
        {
            builder.Append("loadouts=").Append(loadouts != null ? loadouts.Count : 0).Append('|');
            for (int i = 0; loadouts != null && i < loadouts.Count; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel item = loadouts[i];
                builder.Append(item.Index).Append('|')
                    .Append(item.LoadoutId).Append('|')
                    .Append(item.VariantId).Append('|')
                    .Append(item.Visible ? '1' : '0').Append('|')
                    .Append(item.Selected ? '1' : '0').Append('|')
                    .Append(item.ActiveOnSelectedEntity ? '1' : '0').Append('|');
                AppendToken(builder, item.Label);
                AppendToken(builder, item.DetailText);
            }
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
