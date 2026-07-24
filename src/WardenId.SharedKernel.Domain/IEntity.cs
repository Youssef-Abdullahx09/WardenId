namespace WardenId.SharedKernel.Domain;

public interface IEntity : IAudit
{
    Guid Id { get; }
}