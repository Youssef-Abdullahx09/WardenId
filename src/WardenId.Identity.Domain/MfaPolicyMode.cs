namespace WardenId.Identity.Domain;

public enum MfaPolicyMode
{
    Off = 1,
    Optional = 2,
    Required = 3,
    /// <summary>
    /// Enforced like <see cref="Required"/>: WardenId has no roles, so it can't tell admins from
    /// other users, and it fails closed rather than open (see <see cref="MfaPolicyEvaluator.IsMfaRequired"/>).
    /// </summary>
    RequiredForAdmins = 4
}
