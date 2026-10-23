namespace WardenId.Protocol.Application.Subscribers;

/// <summary>CAP consumer groups: this module's prefix plus the topic, so no two modules share a group.</summary>
public static class SubscriberGroups
{
    public const string Prefix = "protocol.";
}
