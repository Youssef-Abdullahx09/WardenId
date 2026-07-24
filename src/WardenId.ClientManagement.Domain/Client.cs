using FluentResults;
using WardenId.ClientManagement.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class Client : AggregateRoot
{
    private Client(
        Guid tenantId,
        ClientId clientId,
        ClientType clientType,
        string displayName,
        string? description,
        GrantType allowedGrantTypes,
        ClientAuthenticationMethod clientAuthenticationMethod,
        bool requirePkce,
        bool isFirstParty,
        bool consentRequired)
    {
        TenantId = tenantId;
        ClientId = clientId;
        ClientType = clientType;
        DisplayName = displayName;
        Description = description;
        AllowedGrantTypes = allowedGrantTypes;
        ClientAuthenticationMethod = clientAuthenticationMethod;
        RequirePkce = requirePkce;
        IsFirstParty = isFirstParty;
        ConsentRequired = consentRequired;
        Enabled = true;
    }

    public Guid TenantId { get; private set; }
    public ClientId ClientId { get; private set; }
    public ClientType ClientType { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; }
    public bool IsFirstParty { get; private set; }
    public GrantType AllowedGrantTypes { get; private set; }
    public bool RequirePkce { get; private set; }
    public TokenLifetimeOverrides? TokenLifetimeOverrides { get; private set; }
    public ClientAuthenticationMethod ClientAuthenticationMethod { get; private set; }
    public bool ConsentRequired { get; private set; }

    private readonly List<RedirectUri> _redirectUris = new();
    public IReadOnlyCollection<RedirectUri> RedirectUris => _redirectUris.AsReadOnly();

    private readonly List<RedirectUri> _postLogoutRedirectUris = new();
    public IReadOnlyCollection<RedirectUri> PostLogoutRedirectUris => _postLogoutRedirectUris.AsReadOnly();

    private readonly List<CorsOrigin> _corsOrigins = new();
    public IReadOnlyCollection<CorsOrigin> CorsOrigins => _corsOrigins.AsReadOnly();

    private readonly List<ClientSecret> _secrets = new();
    public IReadOnlyCollection<ClientSecret> Secrets => _secrets.AsReadOnly();

    private readonly List<ClientSigningKey> _signingKeys = new();
    public IReadOnlyCollection<ClientSigningKey> SigningKeys => _signingKeys.AsReadOnly();

    private readonly List<Guid> _assignedScopeIds = new();
    public IReadOnlyCollection<Guid> AssignedScopeIds => _assignedScopeIds.AsReadOnly();

    public static Result<Client> Create(
        Guid tenantId,
        ClientType clientType,
        string displayName,
        GrantType allowedGrantTypes,
        ClientAuthenticationMethod clientAuthenticationMethod,
        bool isFirstParty,
        bool consentRequired,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(ClientErrors.ClientDisplayNameRequired());

        var trimmed = displayName.Trim();

        if (trimmed.Length > 200) return Result.Fail(ClientErrors.ClientDisplayNameTooLong());

        var requirePkce = clientType == ClientType.Public;

        var client = new Client(
            tenantId,
            ClientId.Generate(),
            clientType,
            trimmed,
            description,
            allowedGrantTypes,
            clientAuthenticationMethod,
            requirePkce,
            isFirstParty,
            consentRequired);

        client.RaiseDomainEvent(new ClientCreatedDomainEvent(client.ClientId));
        return Result.Ok(client);
    }
}
