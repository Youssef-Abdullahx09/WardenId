namespace WardenId.SharedKernel.Domain;

/// <summary>
/// Which admin.* audit channel an IAuditableAggregate's changes belong to. A small, closed
/// enum living here (not in WardenId.Audit.Domain) deliberately — SharedKernel.Domain has
/// zero dependency on any feature module, Audit included. The audit interceptor maps this
/// to the real AuditChannel enum.
/// </summary>
public enum AuditableChannel
{
    AdminDirectory = 1,
    AdminConfig = 2
}
