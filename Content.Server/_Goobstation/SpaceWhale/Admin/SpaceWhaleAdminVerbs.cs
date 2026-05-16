using Content.Server.Administration.Managers;
using Content.Server._Goobstation.SpaceWhale.AI;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server._Goobstation.SpaceWhale.Admin;

public sealed partial class SpaceWhaleAdminVerbs : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WhaleAbilitySystem _ability = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WhaleSpawnedByComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<WhaleSpawnedByComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<ActorComponent>(args.User, out var actor) || !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;

        AddVerb(args, "Force Roar", () => _ability.TryRoar(ent.Owner, 0f));
        AddVerb(args, "Heal Full", () => _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(-100000) } }, true));
        AddVerb(args, "Damage 1000", () => _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(1000) } }, true));
        AddVerb(args, "Toggle Aura", () => Toggle<WhaleAuraComponent>(ent.Owner));
        AddVerb(args, "Toggle Memory", () => Toggle<WhaleMemoryComponent>(ent.Owner));
        AddVerb(args, "Despawn", () => QueueDel(ent.Owner));
    }

    private void AddVerb(GetVerbsEvent<Verb> args, string text, Action act)
    {
        args.Verbs.Add(new Verb
        {
            Text = text,
            Category = VerbCategory.Debug,
            Act = act,
        });
    }

    private void Toggle<T>(EntityUid uid) where T : Component, new()
    {
        if (HasComp<T>(uid))
            RemComp<T>(uid);
        else
            EnsureComp<T>(uid);
    }
}
