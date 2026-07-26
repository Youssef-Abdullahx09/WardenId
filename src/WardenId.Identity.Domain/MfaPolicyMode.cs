namespace WardenId.Identity.Domain;

public enum MfaPolicyMode
{
    Off = 1,
    Optional = 2,
    Required = 3,
    RequiredForAdmins = 4
}
