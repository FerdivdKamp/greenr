# Questionnaire administration and responses (issue #19)

All `/api/questionnaires` endpoints require a valid bearer token. Only the
`QuestionnaireAdmin` role can create definitions or use either publish endpoint.
Registration cannot grant a role. Operators grant administration by configuring
an allowlist of existing account UUIDs in `Questionnaires:AdminUserIds`:

```powershell
dotnet user-secrets set 'Questionnaires:AdminUserIds:0' '<account-userId>' --project backend/CarbonTracker.API
```

Use additional indices for additional administrators. In deployments the
equivalent environment variable is `Questionnaires__AdminUserIds__0`. No users
are administrators by default. Obtain the account ID from `/api/auth/me` after
registration/login. Restart after configuration changes, then login or refresh
to receive a token with the role. Removing a grant affects newly issued tokens;
previous access tokens retain their role until expiry (15 minutes by default).
Protect this configuration as an operator permission boundary.

Ordinary users can list and fetch only `active` definitions; drafts and inactive
versions return 404 by ID. Administrators can list/fetch all versions. The
`/{canonicalId}/latest` endpoint always returns the active version, or 404.

## Versions and publishing

`POST /api/questionnaires` accepts `title`, `definition` (a JSON object), and
optionally `canonicalId` and/or `supersedesId`. The title must be nonblank.
Status must be omitted, null, or `draft`; activation requires publishing. With
no IDs it creates a family at version 1. An existing `canonicalId` or
`supersedesId` creates the next version after the family's current maximum,
even if superseding an older version. Both IDs, if supplied, must agree.
A missing superseded row returns 404. Empty UUIDs return 400.

Creating a draft leaves the current active version and its replacement link
unchanged. Definitions are immutable; corrections require another version.

`POST /api/questionnaires/{id}/publish` activates that version. Alternatively,
`POST /api/questionnaires/families/{canonicalId}/publish` accepts exactly one of
`{"targetId":"..."}`, `{"targetVersion":2}`, or `{"latest":true}`. A target from
another family or ambiguous selectors return 400; missing versions return 404.
Publishing validates the stored definition, inactivates the previous active
version and links its `replaced_by_id` to the target in the same transaction.
The activated version's replacement link is cleared. Republishing is allowed,
including rolling back to an older version. Historical responses retain their
original version, canonical ID, and definition hash.

## Supported validation rules

The server supports a limited SurveyJS subset, rather than executing SurveyJS
expressions. Definitions must contain either a flat `elements` array or `pages`
with flat `elements` arrays. At least one question is required. Question names
must be nonempty and unique across the whole definition. Supported types are
`text`, `comment`, `boolean`, `rating`, `radiogroup`, `dropdown`, and `checkbox`.

- `isRequired` requires a present, nonnull, nonblank answer (nonempty array for
  checkboxes). Optional empty/null values are accepted.
- Text/comment answers must be strings; numeric `maxLength` is enforced.
  Text supports `inputType` of `text`, `number` (JSON number with numeric `min`
  and `max`), or `time` (a valid 24-hour `HH:mm` string).
- Boolean answers must be JSON booleans. Ratings must be integers between
  `rateMin` and `rateMax` (defaults 1 and 5).
- Radio/dropdown answers must equal one configured choice value. Checkbox
  answers must be arrays of unique configured values. Choices may be strings,
  numbers, or objects with a string/numeric `value`; JSON types must match.
- Unknown/duplicate answer keys, wrong types, out-of-range numbers, unsupported
  question types and malformed structures return 400 before any response writes.

Conditional expressions, custom `validators`, panels/matrices, dynamic choices,
custom rating values/steps, custom boolean values, triggers, defaults, alternate
value names, and other/none/select-all choice controls are unsupported and
rejected when present. Time bounds are unsupported. Presentation fields do not
change server validation: requiredness applies regardless of client visibility.
Other SurveyJS rules outside the subset above are not enforced. Authors must
use this contract when publishing; the bundled Swagger create example follows it.

Legacy definitions outside this subset remain readable if already active, but
cannot be published or accept new responses. Create and publish a supported
replacement. The old commented `woon_werk_verkeer.json` prototype includes
unsupported conditional/custom rules and is not a valid upload example.

## Responses and manual checks

`POST /api/questionnaires/{questionnaireId}/responses` accepts
`{"answers":{"dagen_naar_werk":2,"vervoer":["bike"],"reis_tijd_totaal":"00:30"}}`
for the Swagger example. Only active versions accept responses (otherwise 404).
The owner is the nonempty UUID in the validated token's name-identifier claim;
a `userId` supplied in JSON is ignored. Anonymous submission is no longer
supported. Success is 201 with `{"id":"..."}`. Answers and answer items are
stored atomically with a SHA-256 hash of the exact stored definition. Repeated
submissions are allowed; no scoring or client visibility evaluation is performed.

1. Register two accounts. Grant only account A administration using its UUID,
   restart the API, and log in again. Without a token all questionnaire routes
   return 401; account B receives 403 on create and both publish routes.
2. As A, create the Swagger example as a draft. B cannot see it by list or ID.
   Publish it; B can now fetch it and submit valid answers.
3. Submit as B with A's `userId` added to JSON. The persisted response owner
   must still be B. Try missing required answers, unknown keys, wrong types,
   a rating of 8, or an unknown transport choice: expect 400.
4. Create a replacement draft as A: the original remains active. Publish the
   replacement and verify the original is hidden from B and rejects submissions.
   Publish the original again to verify rollback.

Run the HTTP integration tests (isolated DuckDB databases; no local API needed):

```powershell
dotnet test backend/CarbonTracker.API/CarbonTracker.API.sln --filter FullyQualifiedName~QuestionnaireTests
```
