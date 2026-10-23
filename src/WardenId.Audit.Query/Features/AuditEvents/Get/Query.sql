-- One event by id. Ids are unique Guid v7s; the key is (Id, Channel) only because audit.events is partitioned by Channel.
SELECT audit_event."Id",
       audit_event."Channel",
       audit_event."TenantId",
       audit_event."Sequence",
       audit_event."OccurredAt",
       audit_event."Payload",
       audit_event."EventType",
       audit_event."Actor",
       COALESCE(audit_event."Targets", '{}') AS "Targets",
       audit_event."Outcome"
FROM "audit"."events" audit_event
WHERE audit_event."Id" = @EventId
LIMIT 1;
