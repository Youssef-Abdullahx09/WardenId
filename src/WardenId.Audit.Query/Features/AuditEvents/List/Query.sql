-- One page, each row carrying the total matching count in its trailing "RecordsTotal" column (Dapper splits on it).
-- The window count is taken before LIMIT, so it counts every match, not just the page.
-- Every filter is optional (NULL = any) and an exact match on an indexed column; From and To are both inclusive.
-- @Actor matches the scope-qualified actor or the bare actor id. @Target uses array containment (@>), which the GIN
-- index on "Targets" serves ("= ANY(...)" would scan); a NULL "Targets" never matches.
-- The page is ordered by @SortOption (OccurredAt or Sequence), and Id breaks ties in the same direction so pages are
-- stable. Each CASE yields NULL for every row unless its field and direction are the chosen ones. Sequence is per
-- channel and NULL until the event is sealed; unsealed events come last in either direction.
SELECT audit_event."Id",
       audit_event."Channel",
       audit_event."TenantId",
       audit_event."Sequence",
       audit_event."OccurredAt",
       audit_event."Payload",
       audit_event."EventType",
       audit_event."Actor",
       COALESCE(audit_event."Targets", '{}') AS "Targets",
       audit_event."Outcome",
       COUNT(*) OVER () AS "RecordsTotal"
FROM "audit"."events" audit_event
WHERE (@Channel IS NULL OR audit_event."Channel" = @Channel)
  AND (@TenantId IS NULL OR audit_event."TenantId" = @TenantId)
  AND (@From IS NULL OR audit_event."OccurredAt" >= @From)
  AND (@To IS NULL OR audit_event."OccurredAt" <= @To)
  AND (@EventType IS NULL OR audit_event."EventType" = @EventType)
  AND (@Actor IS NULL OR audit_event."Actor" = @Actor OR audit_event."ActorId" = @Actor)
  AND (@Target IS NULL OR audit_event."Targets" @> ARRAY[@Target])
  AND (@Outcome IS NULL OR audit_event."Outcome" = @Outcome)
ORDER BY CASE WHEN @SortOption = 'OccurredAt' AND NOT @Descending THEN audit_event."OccurredAt" END,
         CASE WHEN @SortOption = 'OccurredAt' AND @Descending THEN audit_event."OccurredAt" END DESC,
         CASE WHEN @SortOption = 'Sequence' AND NOT @Descending THEN audit_event."Sequence" END NULLS LAST,
         CASE WHEN @SortOption = 'Sequence' AND @Descending THEN audit_event."Sequence" END DESC NULLS LAST,
         CASE WHEN NOT @Descending THEN audit_event."Id" END,
         CASE WHEN @Descending THEN audit_event."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
