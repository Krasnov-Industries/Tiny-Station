// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Kayzel <43700376+KayzelW@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Trest <144359854+trest100@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 kurokoTurbo <92106367+kurokoTurbo@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Goob._Shitmed.Choice.UI;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared._Goob._Shitmed.Medical.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Goob._Shitmed.Medical.Surgery;

[UsedImplicitly]
public sealed class SurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly SurgerySystem _system;
    [ViewVariables]
    private SurgeryWindow? _window;
    private EntityUid? _part;
    private bool _isBody;
    private (EntityUid Ent, EntProtoId Proto)? _surgery;
    private readonly List<EntProtoId> _previousSurgeries = new();
    private readonly Dictionary<NetEntity, ChoiceControl> _partButtons = new();
    private readonly Dictionary<(BodyPartType Type, BodyPartSymmetry Symmetry), Texture?> _partIconCache = new();

    private static readonly Color TabActiveColor = Color.FromHex("#5bc5ff");
    private static readonly Color TabInactiveColor = Color.FromHex("#9fa9b3");
    private static readonly Color StepCompleteColor = Color.FromHex("#6fdb82");
    private static readonly Color StepPendingColor = Color.FromHex("#ffd166");
    private static readonly Color PartSelectedColor = Color.FromHex("#4cc2ff");

    private static readonly ResPath IconTorso = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/torso.png");
    private static readonly ResPath IconHead = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/head.png");
    private static readonly ResPath IconArmLeft = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/leftarm.png");
    private static readonly ResPath IconArmRight = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/rightarm.png");
    private static readonly ResPath IconHandLeft = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/lefthand.png");
    private static readonly ResPath IconHandRight = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/righthand.png");
    private static readonly ResPath IconLegLeft = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/leftleg.png");
    private static readonly ResPath IconLegRight = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/rightleg.png");
    private static readonly ResPath IconFootLeft = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/leftfoot.png");
    private static readonly ResPath IconFootRight = new("/Textures/_Goob/Shitmed/Interface/Targeting/Doll/rightfoot.png");

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) => _system = _entities.System<SurgerySystem>();

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window is null
            || message is not SurgeryBuiRefreshMessage)
            return;

        RefreshUI();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not SurgeryBuiState s)
            return;

        Update(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void Update(SurgeryBuiState state)
    {
        if (_window == null)
        {
            _window = new SurgeryWindow();
            _window.OnClose += Close;
            _window.Title = Loc.GetString("surgery-ui-window-title");

            _window.PartsButton.OnPressed += _ =>
            {
                _part = null;
                _isBody = false;
                _surgery = null;
                _previousSurgeries.Clear();
                HighlightPart(null);
                View(ViewType.Parts);
            };

            _window.SurgeriesButton.OnPressed += _ =>
            {
                _surgery = null;
                _previousSurgeries.Clear();

                if (!_entities.TryGetNetEntity(_part, out var netPart)
                    || State is not SurgeryBuiState s
                    || !s.Choices.TryGetValue(netPart.Value, out var surgeries))
                    return;

                OnPartPressed(netPart.Value, surgeries);
            };

            _window.StepsButton.OnPressed += _ =>
            {
                if (!_entities.TryGetNetEntity(_part, out var netPart)
                    || _previousSurgeries.Count == 0)
                    return;

                var last = _previousSurgeries[^1];
                _previousSurgeries.RemoveAt(_previousSurgeries.Count - 1);

                if (_system.GetSingleton(last) is not { } previousId
                    || !_entities.TryGetComponent(previousId, out SurgeryComponent? previous))
                    return;

                OnSurgeryPressed((previousId, previous), netPart.Value, last);
            };
        }

        _window.Surgeries.DisposeAllChildren();
        _window.Steps.DisposeAllChildren();
        _window.Parts.DisposeAllChildren();
        View(ViewType.Parts);

        var oldSurgery = _surgery;
        var oldPart = _part;
        _part = null;
        _surgery = null;

        var options = new List<(NetEntity netEntity, EntityUid entity, string Name, BodyPartType? PartType, BodyPartSymmetry? Symmetry)>();
        foreach (var choice in state.Choices.Keys)
            if (_entities.TryGetEntity(choice, out var ent))
            {
                if (_entities.TryGetComponent(ent, out BodyPartComponent? part))
                    options.Add((choice, ent.Value, _entities.GetComponent<MetaDataComponent>(ent.Value).EntityName, part.PartType, part.Symmetry));
                else if (_entities.TryGetComponent(ent, out BodyComponent? body))
                    options.Add((choice, ent.Value, _entities.GetComponent<MetaDataComponent>(ent.Value).EntityName, null, null));
            }

        options.Sort((a, b) =>
        {
            int GetScore(BodyPartType? partType)
            {
                return partType switch
                {
                    BodyPartType.Head => 1,
                    BodyPartType.Torso => 2,
                    BodyPartType.Arm => 3,
                    BodyPartType.Hand => 4,
                    BodyPartType.Leg => 5,
                    BodyPartType.Foot => 6,
                    // BodyPartType.Tail => 8, No tails yet!
                    BodyPartType.Other => 9,
                    _ => 10
                };
            }

            return GetScore(a.PartType) - GetScore(b.PartType);
        });

        _partButtons.Clear();
        HighlightPart(null);

        foreach (var (netEntity, entity, partName, partType, symmetry) in options)
        {
            //var netPart = _entities.GetNetEntity(part.Owner);
            var surgeries = state.Choices[netEntity];
            var partButton = new ChoiceControl();

            partButton.Set(partName, GetPartIcon(partType, symmetry));
            partButton.Button.OnPressed += _ => OnPartPressed(netEntity, surgeries);

            _window.Parts.AddChild(partButton);
            _partButtons[netEntity] = partButton;

            foreach (var surgeryId in surgeries)
            {
                if (_system.GetSingleton(surgeryId) is not { } surgery ||
                    !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
                    continue;

                if (oldPart == entity && oldSurgery?.Proto == surgeryId)
                    OnSurgeryPressed((surgery, surgeryComp), netEntity, surgeryId);
            }

            if (oldPart == entity && oldSurgery == null)
                OnPartPressed(netEntity, surgeries);
        }


        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    private void AddStep(EntProtoId stepId, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null
            || _system.GetSingleton(stepId) is not { } step)
            return;

        var stepName = new FormattedMessage();
        stepName.AddText(_entities.GetComponent<MetaDataComponent>(step).EntityName);
        var stepButton = new SurgeryStepButton { Step = step };
        stepButton.Button.OnPressed += _ => SendPredictedMessage(new SurgeryStepChosenBuiMsg(netPart, surgeryId, stepId, _isBody));

        _window.Steps.AddChild(stepButton);
    }

    private void OnSurgeryPressed(Entity<SurgeryComponent> surgery, NetEntity netPart, EntProtoId surgeryId)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);
        _surgery = (surgery, surgeryId);

        _window.Steps.DisposeAllChildren();

        // This apparently does not consider if theres multiple surgery requirements in one surgery. Maybe thats fine.
        if (surgery.Comp.Requirement is { } requirementId && _system.GetSingleton(requirementId) is { } requirement)
        {
            var label = new ChoiceControl();
            label.Button.OnPressed += _ =>
            {
                _previousSurgeries.Add(surgeryId);

                if (_entities.TryGetComponent(requirement, out SurgeryComponent? requirementComp))
                    OnSurgeryPressed((requirement, requirementComp), netPart, requirementId);
            };

            var msg = new FormattedMessage();
            var surgeryName = _entities.GetComponent<MetaDataComponent>(requirement).EntityName;
            msg.AddMarkup($"[bold]{Loc.GetString("surgery-ui-window-require")}: {surgeryName}[/bold]");
            label.Set(msg, null);

            _window.Steps.AddChild(label);
            _window.Steps.AddChild(new HSeparator { Margin = new Thickness(0, 0, 0, 1) });
        }
        foreach (var stepId in surgery.Comp.Steps)
            AddStep(stepId, netPart, surgeryId);

        View(ViewType.Steps);
        RefreshUI();
    }

    private void OnPartPressed(NetEntity netPart, List<EntProtoId> surgeryIds)
    {
        if (_window == null)
            return;

        _part = _entities.GetEntity(netPart);
        _isBody = _entities.HasComponent<BodyComponent>(_part);
        _window.Surgeries.DisposeAllChildren();

        HighlightPart(netPart);

        var surgeries = new List<(Entity<SurgeryComponent> Ent, EntProtoId Id, string Name)>();
        foreach (var surgeryId in surgeryIds)
        {
            if (_system.GetSingleton(surgeryId) is not { } surgery ||
                !_entities.TryGetComponent(surgery, out SurgeryComponent? surgeryComp))
            {
                continue;
            }

            var name = _entities.GetComponent<MetaDataComponent>(surgery).EntityName;
            surgeries.Add(((surgery, surgeryComp), surgeryId, name));
        }

        surgeries.Sort((a, b) =>
        {
            var priority = a.Ent.Comp.Priority.CompareTo(b.Ent.Comp.Priority);
            if (priority != 0)
                return priority;

            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        foreach (var surgery in surgeries)
        {
            var surgeryButton = new ChoiceControl();
            surgeryButton.Set(surgery.Name, null);

            surgeryButton.Button.OnPressed += _ => OnSurgeryPressed(surgery.Ent, netPart, surgery.Id);
            _window.Surgeries.AddChild(surgeryButton);
        }

        RefreshUI();
        View(ViewType.Surgeries);
    }

    private void RefreshUI()
    {
        if (_window == null
            || !_window.IsOpen
            || _part == null
            || !_entities.HasComponent<SurgeryComponent>(_surgery?.Ent)
            || !_entities.TryGetComponent(_player.LocalEntity, out SurgeryTargetComponent? surgeryComp)
            || !surgeryComp.CanOperate)
            return;

        var next = _system.GetNextStep(Owner, _part.Value, _surgery.Value.Ent, _player.LocalEntity.Value);
        var i = 0;
        foreach (var child in _window.Steps.Children)
        {
            if (child is not SurgeryStepButton stepButton)
                continue;

            var status = StepStatus.Incomplete;
            if (next == null)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i > -next.Value.Step - 1)
                status = StepStatus.Complete;
            else if (next.Value.Step < 0 && i <= -next.Value.Step - 1)
                status = StepStatus.Next;
            else if (next.Value.Surgery.Owner != _surgery.Value.Ent)
                status = StepStatus.Incomplete;
            else if (next.Value.Step == i)
                status = StepStatus.Next;
            else if (i < next.Value.Step)
                status = StepStatus.Complete;

            stepButton.Button.Disabled = status != StepStatus.Next;

            var stepName = new FormattedMessage();
            stepName.AddText(_entities.GetComponent<MetaDataComponent>(stepButton.Step).EntityName);

            if (status == StepStatus.Complete)
                stepButton.Button.Modulate = StepCompleteColor;
            else
            {
                stepButton.Button.Modulate = status == StepStatus.Next ? StepPendingColor : Color.White;
                if (status == StepStatus.Next
                    && !_system.CanPerformStepWithHeld(_player.LocalEntity.Value, Owner, _part.Value, stepButton.Step, false, out var popup))
                    stepButton.ToolTip = popup;
            }

            var texture = _entities.GetComponentOrNull<SpriteComponent>(stepButton.Step)?.Icon?.Default;
            stepButton.Set(stepName, texture);
            i++;
        }
    }

    private void View(ViewType type)
    {
        if (_window == null)
            return;

        _window.PartsButton.Parent!.Margin = new Thickness(0, 0, 0, 10);

        var isParts = type == ViewType.Parts;
        var isSurgeries = type == ViewType.Surgeries;
        var isSteps = type == ViewType.Steps;

        _window.Parts.Visible = isParts;
        _window.PartsButton.Disabled = type == ViewType.Parts;
        UpdateTabVisual(_window.PartsButton, isParts);

        _window.Surgeries.Visible = isSurgeries;
        _window.SurgeriesButton.Disabled = type != ViewType.Steps;
        UpdateTabVisual(_window.SurgeriesButton, isSurgeries);

        _window.Steps.Visible = isSteps;
        _window.StepsButton.Disabled = type != ViewType.Steps || _previousSurgeries.Count == 0;
        UpdateTabVisual(_window.StepsButton, isSteps && _previousSurgeries.Count > 0);
        _window.ViewLabel.Text = type switch
        {
            ViewType.Parts => Loc.GetString("surgery-ui-view-parts"),
            ViewType.Surgeries => Loc.GetString("surgery-ui-view-surgeries"),
            ViewType.Steps => Loc.GetString("surgery-ui-view-steps"),
            _ => Loc.GetString("surgery-ui-window-title")
        };

        if (_entities.TryGetComponent(_part, out MetaDataComponent? partMeta) &&
            _entities.TryGetComponent(_surgery?.Ent, out MetaDataComponent? surgeryMeta))
            _window.Title = $"Surgery - {partMeta.EntityName}, {surgeryMeta.EntityName}";
        else if (partMeta != null)
            _window.Title = $"Surgery - {partMeta.EntityName}";
        else
            _window.Title = "Surgery";
    }

    private static void UpdateTabVisual(Button button, bool active)
    {
        button.Modulate = active ? TabActiveColor : TabInactiveColor;
    }

    private Texture? GetPartIcon(BodyPartType? partType, BodyPartSymmetry? symmetry)
    {
        var resolvedType = partType ?? BodyPartType.Torso;
        var resolvedSymmetry = symmetry ?? BodyPartSymmetry.None;
        var key = (resolvedType, resolvedSymmetry);

        if (_partIconCache.TryGetValue(key, out var cached))
            return cached;

        ResPath? path = resolvedType switch
        {
            BodyPartType.Head => IconHead,
            BodyPartType.Arm => resolvedSymmetry == BodyPartSymmetry.Right ? IconArmRight : IconArmLeft,
            BodyPartType.Hand => resolvedSymmetry == BodyPartSymmetry.Right ? IconHandRight : IconHandLeft,
            BodyPartType.Leg => resolvedSymmetry == BodyPartSymmetry.Right ? IconLegRight : IconLegLeft,
            BodyPartType.Foot => resolvedSymmetry == BodyPartSymmetry.Right ? IconFootRight : IconFootLeft,
            _ => IconTorso
        };

        Texture? texture = null;
        if (path != null && _resourceCache.TryGetResource<TextureResource>(path.Value, out var texResource))
            texture = texResource.Texture;

        _partIconCache[key] = texture;
        return texture;
    }

    private void HighlightPart(NetEntity? selected)
    {
        foreach (var (netEntity, button) in _partButtons)
        {
            var isSelected = selected.HasValue && netEntity.Equals(selected.Value);
            button.SetSelected(isSelected);
            button.Modulate = isSelected ? PartSelectedColor : Color.White;
        }
    }

    private enum ViewType
    {
        Parts,
        Surgeries,
        Steps
    }

    private enum StepStatus
    {
        Next,
        Complete,
        Incomplete
    }
}
