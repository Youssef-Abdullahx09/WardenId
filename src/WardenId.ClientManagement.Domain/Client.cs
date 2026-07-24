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

    public void AddRedirectUri(RedirectUri uri)
    {
        if (!_redirectUris.Contains(uri)) _redirectUris.Add(uri);
    }

    public void RemoveRedirectUri(RedirectUri uri) => _redirectUris.Remove(uri);

    public void AddPostLogoutRedirectUri(RedirectUri uri)
    {
        if (!_postLogoutRedirectUris.Contains(uri)) _postLogoutRedirectUris.Add(uri);
    }

    public void RemovePostLogoutRedirectUri(RedirectUri uri) => _postLogoutRedirectUris.Remove(uri);

    public void AddCorsOrigin(CorsOrigin origin)
    {
        if (!_corsOrigins.Contains(origin)) _corsOrigins.Add(origin);
    }

    public void RemoveCorsOrigin(CorsOrigin origin) => _corsOrigins.Remove(origin);

    public bool ValidateRedirectUri(RedirectUri requested) => _redirectUris.Contains(requested);

    public Result AddSecret(ClientSecret secret)
    {
        if (ClientType == ClientType.Public) return Result.Fail(ClientErrors.PublicClientCannotHaveSecret());

        _secrets.Add(secret);
        return Result.Ok();
    }

    public Result RotateSecret(ClientSecret newSecret, DateTimeOffset overlapExpiresAt)
    {
        var addResult = AddSecret(newSecret);
        if (addResult.IsFailed) return addResult;

        foreach (var existing in _secrets.Where(s => s.Id != newSecret.Id && s.ExpiresAt is null))
        {
            existing.SetExpiry(overlapExpiresAt);
        }

        RaiseDomainEvent(new ClientSecretRotatedDomainEvent(newSecret.Id));
        return Result.Ok();
    }

    public Result ExpireSecret(Guid secretId)
    {
        var secret = _secrets.FirstOrDefault(s => s.Id == secretId);
        if (secret is null) return Result.Fail(ClientErrors.SecretNotFound());

        secret.SetExpiry(DateTimeOffset.UtcNow);
        RaiseDomainEvent(new ClientSecretExpiredDomainEvent(secretId));
        return Result.Ok();
    }

    public Result AddSigningKey(ClientSigningKey key)
    {
        if (ClientAuthenticationMethod != ClientAuthenticationMethod.PrivateKeyJwt)
            return Result.Fail(ClientErrors.SigningKeyRequiresPrivateKeyJwt());

        _signingKeys.Add(key);
        return Result.Ok();
    }

    public void AssignScope(Guid scopeId)
    {
        if (_assignedScopeIds.Contains(scopeId)) return;

        _assignedScopeIds.Add(scopeId);
        RaiseDomainEvent(new ClientScopesChangedDomainEvent(scopeId, Assigned: true));
    }

    public void RemoveScope(Guid scopeId)
    {
        if (!_assignedScopeIds.Remove(scopeId)) return;

        RaiseDomainEvent(new ClientScopesChangedDomainEvent(scopeId, Assigned: false));
    }

    public void Disable()
    {
        Enabled = false;
        RaiseDomainEvent(new ClientDisabledDomainEvent());
    }

    public void Enable() => Enabled = true;

    public void UpdateTokenLifetimeOverrides(TokenLifetimeOverrides? overrides) => TokenLifetimeOverrides = overrides;
}
