namespace Content.Shared._Goob._Shitmed.Surgery;

public sealed class SurgerySanitizationEvent : HandledEntityEventArgs;

public sealed class SurgeryPainEvent : CancellableEntityEventArgs;

public sealed class SurgeryIgnorePreviousStepsEvent : HandledEntityEventArgs;
