-- Two result sets: the total matching count, then one page (newest OccurredAt first; Id breaks ties so pages are stable).
-- Every filter is optional (NULL = any) and an exact match on an indexed column; From and To are both inclusive.
-- @Actor matches the scope-qualified actor or the bare actor id. @Target uses array containment (@>), which the GIN
-- index on "Targets" serves ("= ANY(...)" would scan); a NULL "Targets" never matches.
-- The filter is repeated verbatim in both statements.
SELECT COUNT(*)
FROM "audit"."events" audit_event
WHERE (@Channel IS NULL OR audit_event."Channel" = @Channel)
  AND (@TenantId IS NULL OR audit_event."TenantId" = @TenantId)
  AND (@From IS NULL OR audit_event."OccurredAt" >= @From)
  AND (@To IS NULL OR audit_event."OccurredAt" <= @To)
  AND (@EventType IS NULL OR audit_event."EventType" = @EventType)
  AND (@Actor IS NULL OR audit_event."Actor" = @Actor OR audit_event."ActorId" = @Actor)
  AND (@Target IS NULL OR audit_event."Targets" @> ARRAY[@Target])
  AND (@Outcome IS NULL OR audit_event."Outcome" = @Outcome);

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
WHERE (@Channel IS NULL OR audit_event."Channel" = @Channel)
  AND (@TenantId IS NULL OR audit_event."TenantId" = @TenantId)
  AND (@From IS NULL OR audit_event."OccurredAt" >= @From)
  AND (@To IS NULL OR audit_event."OccurredAt" <= @To)
  AND (@EventType IS NULL OR audit_event."EventType" = @EventType)
  AND (@Actor IS NULL OR audit_event."Actor" = @Actor OR audit_event."ActorId" = @Actor)
  AND (@Target IS NULL OR audit_event."Targets" @> ARRAY[@Target])
  AND (@Outcome IS NULL OR audit_event."Outcome" = @Outcome)
ORDER BY audit_event."OccurredAt" DESC, audit_event."Id" DESC
LIMIT @PageSize OFFSET @Offset;
