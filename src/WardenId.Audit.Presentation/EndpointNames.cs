namespace WardenId.Audit.Presentation;

/// <summary>
/// Endpoint names (<c>.WithName</c>), which become OpenAPI <c>operationId</c>s and so the
/// function names in generated clients. Unique across the Gateway; renaming one is a breaking
/// change for those clients.
/// </summary>
public static class EndpointNames
{
    public static class CheckpointKeys
    {
        public const string List = "ListCheckpointKeys";
    }

    public static class Proofs
    {
        public const string Consistency = "GetConsistencyProof";
        public const string Inclusion = "GetInclusionProof";
    }

    public static class Verification
    {
        public const string Verify = "VerifyChannel";
    }
}
