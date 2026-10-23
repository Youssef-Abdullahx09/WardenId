using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Audit.Presentation.Utilities;

/// <summary>Route templates of the audit API, under the versioned <c>/api/v{version}/audit</c> prefix.</summary>
public static class EndpointRoutes
{
    private const string Base = ApiRoutes.VersionedPrefix + "/audit";
    private const string Channel = Base + "/channels/{channel}";

    public static class CheckpointKeys
    {
        public const string List = Base + "/checkpoint-keys";
        public const string Rotate = List + "/rotate";
    }

    public static class Events
    {
        public const string List = Base + "/events";
        public const string Get = List + "/{eventId:guid}";
    }

    public static class Proofs
    {
        public const string Consistency = Channel + "/consistency-proof";
        public const string Inclusion = Events.Get + "/inclusion-proof";
    }

    public static class Verification
    {
        public const string Verify = Channel + "/verification";
    }
}
