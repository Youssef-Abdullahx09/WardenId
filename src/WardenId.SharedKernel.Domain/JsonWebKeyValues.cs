namespace WardenId.SharedKernel.Domain;

/// <summary>JSON Web Key members and values (RFC 7517/7518) used by every module that publishes or reads keys.</summary>
public static class JsonWebKeyValues
{
    public static class Algorithms
    {
        public const string Es256 = "ES256";
    }

    public static class KeyTypes
    {
        public const string Ec = "EC";
    }

    public static class Curves
    {
        public const string P256 = "P-256";
    }

    public static class Uses
    {
        public const string Signature = "sig";
    }

    /// <summary>EC public-key coordinates (RFC 7518 §6.2.1).</summary>
    public static class PublicMembers
    {
        public const string X = "x";
        public const string Y = "y";
    }

    /// <summary>RFC 7518 §6 private-key members (EC/OKP: d; RSA: d, p, q, dp, dq, qi, oth; oct: k).</summary>
    public static class PrivateMembers
    {
        public const string D = "d";
        public const string P = "p";
        public const string Q = "q";
        public const string DP = "dp";
        public const string DQ = "dq";
        public const string QI = "qi";
        public const string Oth = "oth";
        public const string K = "k";
    }
}
