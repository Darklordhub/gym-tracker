# Exercise Media Studio AI Generation Enablement

Use this runbook to enable manual Exercise Media Studio video generation in staging first, then production. Generation is disabled by default and must remain disabled until the checks below pass.

This runbook does not enable automatic generation, automatic refresh, or automatic publishing. Every generation, review, and publish action remains an explicit Admin action.

## Pre-deployment checklist

- Deploy the latest application image and Compose definition.
- Confirm the `AddExerciseMediaGenerationAttempts` migration has been applied. The API applies EF Core migrations during startup; verify the API startup logs show no migration failure.
- Confirm `gym-tracker-api`, `gym-tracker-web`, and `gym-tracker-db` are healthy in Portainer or with `docker compose ps`.
- Confirm the persistent media volume named by `EXERCISE_MEDIA_VOLUME_NAME` exists and is attached to `gym-tracker-api` at `/app/media`.
- Confirm the web proxy exposes `/media` through the API only. It must serve the public published-media area, not the private draft area.
- Confirm draft preview requests use the Admin-only `/api/admin/exercise-catalog/media-studio/{draftId}/video` or `/thumbnail` endpoints.
- Keep `MEDIA_GENERATION_ENABLED=false` for the initial deployment and route validation.
- Set `APP_BASE_URL` to the externally reachable HTTPS origin with no trailing slash, for example `https://staging.example.com`.
- Set a strong `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY` (the JWT secret).
- Confirm no OpenAI key is committed to Git. Store `OPENAI_API_KEY` only in Portainer secrets or the deployment environment.
- Configure provider-side budget, usage alerts, and rate limits before setting `MEDIA_GENERATION_ENABLED=true`.

To verify the migration interactively:

```bash
docker compose exec -it gym-tracker-db sh
psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
WHERE "MigrationId" LIKE '%AddExerciseMediaGenerationAttempts%';
```

The query should return a migration ending in `AddExerciseMediaGenerationAttempts`.

## Required environment variables

| Variable | Purpose |
| --- | --- |
| `MEDIA_GENERATION_ENABLED` | Enables the provider. Keep `false` until staging checks pass. |
| `OPENAI_API_KEY` | Provider API key. Set only in the deployment secret store. |
| `OPENAI_VIDEO_MODEL` | Allowed model: `sora-2` or `sora-2-pro`. |
| `OPENAI_VIDEO_SECONDS` | Allowed duration: `4`, `8`, or `12`. |
| `OPENAI_VIDEO_SIZE` | Allowed size: `720x1280`, `1280x720`, `1024x1792`, or `1792x1024`. |
| `OPENAI_TIMEOUT_SECONDS` | Provider request timeout in seconds. |
| `MEDIA_GENERATION_MAX_JOBS_PER_DAY` | Maximum paid generation starts across the application in the last 24 hours. |
| `MEDIA_GENERATION_MAX_JOBS_PER_HOUR` | Maximum paid generation starts across the application in the last hour. |
| `MEDIA_GENERATION_MAX_JOBS_PER_DRAFT_PER_DAY` | Maximum paid generation starts for one draft in the last 24 hours. |
| `MEDIA_GENERATION_COOLDOWN_SECONDS` | Minimum interval between paid starts for the same draft. |
| `APP_BASE_URL` | External origin used to build public published-media URLs. No trailing slash. |

`MEDIA_GENERATION_ENABLED` remains `false` in the repository defaults and Docker Compose defaults. Never place the key in `appsettings.json`, `.env.example`, Git, screenshots, browser code, or client-side configuration.

## Recommended staging values

Use these values for a controlled staging test:

```dotenv
APP_BASE_URL=https://staging.example.com
MEDIA_GENERATION_ENABLED=true
MEDIA_GENERATION_PROVIDER=OpenAI
OPENAI_API_KEY=replace-with-a-secret-managed-in-portainer
OPENAI_VIDEO_MODEL=sora-2
OPENAI_VIDEO_SECONDS=4
OPENAI_VIDEO_SIZE=1280x720
OPENAI_TIMEOUT_SECONDS=60
MEDIA_GENERATION_MAX_JOBS_PER_DAY=2
MEDIA_GENERATION_MAX_JOBS_PER_HOUR=1
MEDIA_GENERATION_MAX_JOBS_PER_DRAFT_PER_DAY=1
MEDIA_GENERATION_COOLDOWN_SECONDS=300
```

For production, start with generation disabled, complete the staging test, then use the same conservative limits for a small pilot. Increase limits only after reviewing provider-side usage and the generation-attempt audit records.

## Portainer environment block

Add the following values to the Portainer stack environment. The API key line is a placeholder only; enter the real value in Portainer's secret/environment UI and do not paste it into source control.

```dotenv
APP_BASE_URL=https://staging.example.com
MEDIA_GENERATION_ENABLED=true
MEDIA_GENERATION_PROVIDER=OpenAI
OPENAI_API_KEY=replace-with-provider-secret
OPENAI_VIDEO_MODEL=sora-2
OPENAI_VIDEO_SECONDS=4
OPENAI_VIDEO_SIZE=1280x720
OPENAI_TIMEOUT_SECONDS=60
MEDIA_GENERATION_MAX_JOBS_PER_DAY=2
MEDIA_GENERATION_MAX_JOBS_PER_HOUR=1
MEDIA_GENERATION_MAX_JOBS_PER_DRAFT_PER_DAY=1
MEDIA_GENERATION_COOLDOWN_SECONDS=300
```

## Provider-side controls

Application limits protect the application from accidental sequential requests, but they are not a replacement for provider controls. Before enabling generation, configure the OpenAI project or organization with:

- A spend budget or credit limit appropriate for the environment.
- Usage alerts at conservative thresholds.
- Provider-side rate limits where available.
- Access controls so only the deployment operator can change the provider key or budget.

Review usage after every staging or pilot test. If provider controls and application limits disagree, treat the stricter setting as the effective limit.

## Controlled staging test

Use one Admin account and one exercise at a time.

1. Sign in as an Admin and open `/admin`.
2. Create or select one exercise, then create one Video media draft.
3. Confirm the prompt and source snapshot are correct before spending provider credit.
4. Select **Generate** once and accept the confirmation.
5. Select **Refresh status** manually until the provider completes or fails. Do not automate refreshes.
6. Confirm the generated draft video preview works while authenticated as an Admin.
7. In an anonymous browser session, confirm the Admin preview API returns `401` or `403` and a guessed legacy/private draft URL returns `404` or `403`.
8. Review the completed draft, then explicitly approve it.
9. Explicitly publish the approved draft.
10. Confirm the Exercise Library uses the published media and the resulting public `/media/exercises/{exerciseId}/published/...` URL returns `200`.
11. Confirm the old private draft URL still fails anonymously after publication.
12. With the recommended staging limits, create a second eligible draft and try another generation within one hour. Confirm the application returns `Generation limit reached. Try again later.` before a provider call is made.

Publishing copies approved media into the public storage area and updates only the catalog local media override fields. It does not alter provider media URLs and does not publish automatically.

## Rollback plan

1. Set `MEDIA_GENERATION_ENABLED=false` in Portainer or the deployment environment.
2. Redeploy the backend or redeploy the stack so the setting takes effect.
3. Leave already-published media in place. Disabling generation does not remove published media.
4. Do not delete the media volume during a generation rollback.
5. If a specific published asset must be withdrawn, clear that exercise's `LocalVideoUrlOverride` and/or `LocalThumbnailUrlOverride` through the Admin workflow or a carefully reviewed SQL change.
6. Keep `AddExerciseMediaGenerationAttempts` applied. Do not roll back the database migration unless instructed as part of a separately reviewed database recovery plan.

## Troubleshooting

| Symptom | Check and action |
| --- | --- |
| Generate says the provider is disabled | Confirm `MEDIA_GENERATION_ENABLED=true` in the API container environment, then redeploy the API. |
| Generate reports a missing API key | Set `OPENAI_API_KEY` only in the deployment secret store, verify it is available to `gym-tracker-api`, then redeploy. |
| Published media URL points to the wrong host | Correct `APP_BASE_URL` to the external origin without a trailing slash, then regenerate or republish the affected draft. |
| Public `/media` URL returns `404` | Verify the published draft completed successfully, the media volume is attached, and `frontend/nginx.conf` still proxies `/media/` to the API. |
| Draft preview returns `401` or `403` | This is expected for anonymous or non-Admin users. Sign in as an Admin and verify the bearer token is current. |
| Generate returns `Generation limit reached` | Review `ExerciseMediaGenerationAttempts`, wait for the configured window/cooldown, or raise a limit only after reviewing provider budget and recent usage. |
| Draft remains `Generating` | Use **Refresh status** manually. Check the provider job ID and API logs; if the provider reports a failure, refresh marks the draft as `Failed`. |
| Migration is not applied | Check API startup logs and database connectivity. The API runs migrations at startup; do not enable generation until readiness is healthy. |
| Media volume permission or storage error | Confirm the named volume is mounted at `/app/media`, is persistent, and is writable by the API container. Do not expose that volume directly through a host web server. |

## Optional verification commands

Run these from the deployment directory. They do not print the OpenAI key.

```bash
docker compose config
docker compose ps
docker compose logs --tail=200 gym-tracker-api
docker volume inspect gym-tracker_gym-tracker-exercise-media
```

Review tracked configuration references without opening a local `.env` file:

```bash
git grep -n --fixed-strings 'OPENAI_API_KEY' -- \
  ':!docs/exercise-media-studio-enablement.md' \
  ':!.env.example'
```

The expected tracked references are deployment placeholders such as `docker-compose.yml`; there must be no literal provider key value in Git.

After creating a draft, substitute the IDs and URL values below. Do not use a real Admin token in shell history on a shared host.

```bash
# Anonymous Admin preview must not be accessible.
curl -i "$APP_BASE_URL/api/admin/exercise-catalog/media-studio/$DRAFT_ID/video"

# A guessed private/legacy draft path must not be publicly served.
curl -I "$APP_BASE_URL/media/exercises/$EXERCISE_ID/drafts/$DRAFT_ID/video.mp4"

# Copy the LocalVideoUrlOverride value after publication. It should be public.
curl -I "$PUBLISHED_MEDIA_URL"
```

Expected results are `401` or `403` for the Admin preview without an Admin token, `404` or `403` for the private draft path, and `200` for the public published-media URL. Before deployment, the backend test suite should pass:

```bash
dotnet test GymTracker.sln
```
