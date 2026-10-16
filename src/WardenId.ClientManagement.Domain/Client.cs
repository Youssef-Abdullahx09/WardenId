using FluentResults;
using WardenId.ClientManagement.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class Client : AggregateRoot
{
    // Matches the column length in ClientConfiguration.
    public const int DescriptionMaxLength = 1000;

    private const GrantType AllGrantTypes =
        GrantType.AuthorizationCode | GrantType.ClientCredentials | GrantType.RefreshToken | GrantType.DeviceCode;

    private Client(
        Guid tenantId,
        ClientId clientId,
        ClientType clientType,
        string displayName,
        string? description,
        GrantType grantType,
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
        GrantType = grantType;
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
    public GrantType GrantType { get; private set; }
    public bool RequirePkce { get; private set; }
    public TokenLifetimeOverrides? TokenLifetimeOverrides { get; private set; }
    public ClientAuthenticationMethod ClientAuthenticationMethod { get; private set; }
    public bool ConsentRequired { get; private set; }
    public BackchannelLogoutUri? BackchannelLogoutUri { get; private set; }

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
        string? description = null,
        ClientId? clientId = null)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(ClientErrors.ClientDisplayNameRequired());

        var trimmed = displayName.Trim();

        if (trimmed.Length > 200) return Result.Fail(ClientErrors.ClientDisplayNameTooLong());

        var descriptionResult = NormalizeDescription(description);
        if (descriptionResult.IsFailed) return Result.Fail(descriptionResult.Errors);

        var grantsResult = ValidateGrantTypes(clientType, allowedGrantTypes);
        if (grantsResult.IsFailed) return Result.Fail(grantsResult.Errors);

        var requirePkce = clientType == ClientType.Public;

        var client = new Client(
            tenantId,
            clientId ?? ClientId.Generate(),
            clientType,
            trimmed,
            descriptionResult.Value,
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

    // Diff for the same reason as ReplacePostLogoutRedirectUris. An empty set is allowed: Add/Remove can
    // already empty it and Create starts empty, and with no URIs every authorize request fails closed.
    public void ReplaceRedirectUris(IReadOnlyCollection<RedirectUri> uris)
    {
        _redirectUris.RemoveAll(existing => !uris.Contains(existing));

        foreach (var uri in uris)
        {
            AddRedirectUri(uri);
        }
    }

    public void AddPostLogoutRedirectUri(RedirectUri uri)
    {
        if (!_postLogoutRedirectUris.Contains(uri)) _postLogoutRedirectUris.Add(uri);
    }

    public void RemovePostLogoutRedirectUri(RedirectUri uri) => _postLogoutRedirectUris.Remove(uri);

    // Diff instead of clear-and-add: removing and re-adding an equal owned entry in one SaveChanges would
    // track two entities with the same (ClientId, Value) key.
    public void ReplacePostLogoutRedirectUris(IReadOnlyCollection<RedirectUri> uris)
    {
        _postLogoutRedirectUris.RemoveAll(existing => !uris.Contains(existing));

        foreach (var uri in uris)
        {
            AddPostLogoutRedirectUri(uri);
        }
    }

    public void SetBackchannelLogoutUri(BackchannelLogoutUri? uri)
    {
        if (BackchannelLogoutUri == uri) return;

        BackchannelLogoutUri = uri;
        RaiseDomainEvent(new ClientBackchannelLogoutUriChangedDomainEvent(uri?.Value));
    }

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

    public Result UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Result.Fail(ClientErrors.ClientDisplayNameRequired());

        var trimmed = displayName.Trim();

        if (trimmed.Length > 200) return Result.Fail(ClientErrors.ClientDisplayNameTooLong());

        DisplayName = trimmed;
        return Result.Ok();
    }

    public void UpdateConsentRequired(bool consentRequired) => ConsentRequired = consentRequired;

    /// <summary>Sets the description; null, empty or whitespace clears it.</summary>
    public Result UpdateDescription(string? description)
    {
        var descriptionResult = NormalizeDescription(description);
        if (descriptionResult.IsFailed) return Result.Fail(descriptionResult.Errors);

        Description = descriptionResult.Value;
        return Result.Ok();
    }

    public void UpdateIsFirstParty(bool isFirstParty) => IsFirstParty = isFirstParty;

    public Result UpdateGrantTypes(GrantType grantTypes)
    {
        var grantsResult = ValidateGrantTypes(ClientType, grantTypes);
        if (grantsResult.IsFailed) return grantsResult;

        if (GrantType == grantTypes) return Result.Ok();

        var previous = GrantType;
        GrantType = grantTypes;
        RaiseDomainEvent(new ClientGrantTypesChangedDomainEvent(previous, grantTypes));
        return Result.Ok();
    }

    private static Result<string?> NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return Result.Ok<string?>(null);

        var trimmed = description.Trim();

        return trimmed.Length > DescriptionMaxLength
            ? Result.Fail(ClientErrors.ClientDescriptionTooLong())
            : Result.Ok<string?>(trimmed);
    }

    private static Result ValidateGrantTypes(ClientType clientType, GrantType grantTypes)
    {
        if (grantTypes == default) return Result.Fail(ClientErrors.GrantTypeRequired());

        if ((grantTypes & ~AllGrantTypes) != 0) return Result.Fail(ClientErrors.GrantTypeUnknown());

        // A public client has no credentials, so client_credentials would mean "anyone holding the client_id".
        if (clientType == ClientType.Public && grantTypes.HasFlag(GrantType.ClientCredentials))
            return Result.Fail(ClientErrors.PublicClientCannotUseClientCredentials());

        // Refresh tokens are only issued alongside a user's tokens (UserTokenGrantFactory).
        if (grantTypes.HasFlag(GrantType.RefreshToken)
            && !grantTypes.HasFlag(GrantType.AuthorizationCode)
            && !grantTypes.HasFlag(GrantType.DeviceCode))
            return Result.Fail(ClientErrors.RefreshTokenRequiresUserGrant());

        return Result.Ok();
    }
}
