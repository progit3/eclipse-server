using System.Linq;
using Content.Client.Gameplay;
using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Verbs;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.InteractionVerbs;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Emotes;

[UsedImplicitly]
public sealed class DirectedEmotesUIController : UIController, IOnStateChanged<GameplayState>
{
    private const float InteractionRange = 3f;
    private const int MaxTargets = 6;
    private const float RangeCheckInterval = 0.5f;

    [Dependency] private readonly IPlayerManager _player = default!;

    private SimpleRadialMenu? _menu;
    private readonly Dictionary<EntityUid, string> _trackedTargets = new();
    private float _rangeCheckTimer;

    public void OnStateEntered(GameplayState state)
    {
    }

    public void OnStateExited(GameplayState state)
    {
        CloseMenu();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_menu == null || _trackedTargets.Count == 0)
            return;

        _rangeCheckTimer -= args.DeltaSeconds;
        if (_rangeCheckTimer > 0f)
            return;

        _rangeCheckTimer = RangeCheckInterval;
        CheckTrackedTargets();
    }

    public void OpenDirectedEmotesMenu(EntityUid? focusedTarget = null)
    {
        if (_menu != null)
            CloseMenu();

        var targets = GetTargets(focusedTarget);
        if (targets.Count == 0)
        {
            Popup(Loc.GetString("directed-emotes-menu-no-targets"));
            return;
        }

        var models = ConvertToButtons(targets);
        if (models.Count == 0)
        {
            Popup(Loc.GetString("directed-emotes-menu-no-actions"));
            return;
        }

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(models);
        _menu.OpenCentered();
        _menu.OnClose += OnMenuClosed;

        _rangeCheckTimer = RangeCheckInterval;
    }

    private List<EntityUid> GetTargets(EntityUid? focusedTarget)
    {
        if (_player.LocalEntity is not { } user)
            return new();

        var userCoords = EntityManager.GetComponent<TransformComponent>(user).Coordinates;
        var targets = new List<(EntityUid Uid, float Distance)>();

        if (focusedTarget is { } focused
            && focused != user
            && EntityManager.HasComponent<ActorComponent>(focused)
            && TryGetTargetDistance(userCoords, focused, out var focusedDistance)
            && focusedDistance <= InteractionRange)
        {
            targets.Add((focused, focusedDistance));
        }

        var lookup = EntityManager.System<EntityLookupSystem>();
        foreach (var ent in lookup.GetEntitiesInRange<ActorComponent>(userCoords, InteractionRange, LookupFlags.Dynamic))
        {
            var target = ent.Owner;
            if (target == user)
                continue;

            if (targets.Any(pair => pair.Uid == target))
                continue;

            if (!TryGetTargetDistance(userCoords, target, out var distance))
                continue;

            targets.Add((target, distance));
        }

        targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return targets.Take(MaxTargets).Select(pair => pair.Uid).ToList();
    }

    private bool TryGetTargetDistance(EntityCoordinates userCoords, EntityUid target, out float distance)
    {
        distance = default;
        return EntityManager.TryGetComponent(target, out TransformComponent? xform)
            && xform.Coordinates.TryDistance(EntityManager, userCoords, out distance);
    }

    private List<RadialMenuOptionBase> ConvertToButtons(List<EntityUid> targets)
    {
        var verbSystem = EntityManager.System<VerbSystem>();
        var models = new List<RadialMenuOptionBase>();
        _trackedTargets.Clear();

        if (_player.LocalEntity is not { } user)
            return models;

        foreach (var target in targets)
        {
            var verbs = verbSystem.GetLocalVerbs(target, user, typeof(InteractionVerb))
                .Where(verb => verb.Category?.Text == VerbCategory.Interact.Text && !verb.Disabled)
                .Take(8)
                .Select(verb => new RadialMenuActionOption<Verb>(
                    selectedVerb => verbSystem.ExecuteVerb(target, selectedVerb),
                    verb)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(verb.Icon),
                    ToolTip = verb.Text
                })
                .Cast<RadialMenuOptionBase>()
                .ToList();

            if (verbs.Count == 0)
                continue;

            var name = Identity.Name(target, EntityManager);
            _trackedTargets[target] = name;
            models.Add(new RadialMenuNestedLayerOption(verbs)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(target),
                ToolTip = name
            });
        }

        return models;
    }

    private void CheckTrackedTargets()
    {
        if (_player.LocalEntity is not { } user)
            return;

        var userCoords = EntityManager.GetComponent<TransformComponent>(user).Coordinates;

        foreach (var (target, name) in _trackedTargets.ToArray())
        {
            if (!EntityManager.EntityExists(target)
                || !EntityManager.GetComponent<TransformComponent>(target).Coordinates.TryDistance(EntityManager, userCoords, out var distance)
                || distance > InteractionRange)
            {
                AddChatNotice(Loc.GetString("directed-emotes-menu-player-left-range", ("player", name)));
                _trackedTargets.Remove(target);
            }
        }
    }

    private void AddChatNotice(string message)
    {
        var wrapped = FormattedMessage.EscapeText(message);
        var chat = UIManager.GetUIController<ChatUIController>();
        chat.ProcessChatMessage(new ChatMessage(ChatChannel.Notifications, message, wrapped, NetEntity.Invalid, null), false);
    }

    private void Popup(string message)
    {
        if (_player.LocalEntity is not { } user)
            return;

        EntityManager.System<PopupSystem>().PopupEntity(message, user);
    }

    private void OnMenuClosed()
    {
        CloseMenu();
    }

    private void CloseMenu()
    {
        if (_menu != null)
        {
            _menu.OnClose -= OnMenuClosed;
            _menu.Dispose();
            _menu = null;
        }

        _trackedTargets.Clear();
    }
}
