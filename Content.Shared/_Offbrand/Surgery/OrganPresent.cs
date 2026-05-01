using Content.Shared.Body;
using Content.Shared.Construction;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._Offbrand.Surgery;

/// <summary>
/// Condition that passes when the body has (or, when <see cref="ShouldHave"/> is false, lacks) an organ of the given category.
/// </summary>
[DataDefinition]
public sealed partial class OrganPresent : IGraphCondition
{
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Category;

    [DataField]
    public bool ShouldHave = true;

    public bool Condition(EntityUid uid, IEntityManager entityManager)
    {
        return HasOrgan(uid, entityManager) == ShouldHave;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var has = HasOrgan(args.Examined, entityManager);

        switch (ShouldHave)
        {
            case true when !has:
                args.PushMarkup(Loc.GetString("construction-examine-organ-should-have", ("category", CategoryName())));
                return true;
            case false when has:
                args.PushMarkup(Loc.GetString("construction-examine-organ-should-not-have", ("category", CategoryName())));
                return true;
        }

        return false;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry()
        {
            Localization = ShouldHave
                ? "construction-step-condition-organ-should-have"
                : "construction-step-condition-organ-should-not-have",
            Arguments = [("category", CategoryName())],
        };
    }

    private bool HasOrgan(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<BodyComponent>(uid, out var body) || body.Organs is null)
            return false;

        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (entityManager.TryGetComponent<OrganComponent>(contained, out var organ) && organ.Category == Category)
                return true;
        }

        return false;
    }

    private string CategoryName()
    {
        var key = $"organ-category-{Category.Id.ToLowerInvariant()}";
        return Loc.GetString(key);
    }
}
