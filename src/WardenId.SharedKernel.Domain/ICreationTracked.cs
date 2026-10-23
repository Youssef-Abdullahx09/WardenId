namespace WardenId.SharedKernel.Domain;

/// <summary>
/// An entity whose creation time is kept on its row, for listing and sorting. DbContextBase stamps
/// CreatedAt on insert. Who created it, and every later change, is in the Audit module's log.
/// </summary>
public interface ICreationTracked
{
    DateTimeOffset CreatedAt { get; }
}
