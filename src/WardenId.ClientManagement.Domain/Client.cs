using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class Client : AggregateRoot
{
    public Client()
    {
        
    }
    public Guid TenantId { get; private set; }
    public string DisplayName { get; private set; }
    public GrantType GrantType { get; private set; }
    public bool RequirePkce { get; private set; }

}