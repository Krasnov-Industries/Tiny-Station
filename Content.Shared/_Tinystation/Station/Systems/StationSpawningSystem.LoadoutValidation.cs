#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Station.Systems;

public sealed partial class StationSpawningSystem
{
    [Dependency] private IDependencyCollection _dependencies = default!;
}
