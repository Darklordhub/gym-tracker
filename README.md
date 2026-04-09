# Gym Tracker

## Portainer Deployment

This repo includes a Portainer-ready stack in [`docker-compose.yml`](./docker-compose.yml).

### Required variables

Set these values in Portainer when deploying the stack, or place them in a local `.env` file before running `docker compose`:

- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_VOLUME_NAME`
- `POSTGRES_BACKUP_VOLUME_NAME`
- `JWT_SIGNING_KEY`
- `APP_BASE_URL`
- `WEB_PORT`

Optional backup/auth settings:

- `POSTGRES_BACKUP_RETENTION_COUNT` default `7`
- `POSTGRES_BACKUP_INTERVAL_SECONDS` default `86400` (daily)
- `JWT_ISSUER` default `gym-tracker-api`
- `JWT_AUDIENCE` default `gym-tracker-app`
- `JWT_EXPIRATION_HOURS` default `24`
- `LEGACY_USER_EMAIL` blank by default
- `LEGACY_USER_PASSWORD` blank by default

Example values are provided in [`/.env.example`](./.env.example).

### Deploy in Portainer

1. Open `Stacks` in Portainer.
2. Create a new stack.
3. Paste the contents of [`docker-compose.yml`](./docker-compose.yml).
4. Add the environment variables from [`/.env.example`](./.env.example) and replace the example password and URL values.
5. Deploy the stack.

### Authentication

- New users can create an account on `/register` and then log in on `/login`.
- The API uses JWT bearer authentication and the frontend stores the issued token in browser local storage for this app.
- After login, all weight entries, workouts, workout templates, active sessions, and goals are filtered by the authenticated user on the server side.
- Logging out clears the stored token from the browser.

### Persistence

PostgreSQL data is stored in the Docker volume named by `POSTGRES_VOLUME_NAME`.

The default is `gym-tracker_gym-tracker-postgres-data`, which matches the volume name created by the current stack name and prevents redeploys from silently switching to a fresh empty volume when the Compose project name changes.

Recreating containers will not remove database data unless that volume is explicitly deleted.

Backups are stored separately in the Docker volume named by `POSTGRES_BACKUP_VOLUME_NAME`, so dump files persist outside the database container and survive container recreation.

### Automatic PostgreSQL backups

The stack includes a dedicated `gym-tracker-db-backup` sidecar container that runs `pg_dump` against `gym-tracker-db` once every `POSTGRES_BACKUP_INTERVAL_SECONDS`.

- Backup files are written to `/backups` inside the backup container, backed by the persistent Docker volume set by `POSTGRES_BACKUP_VOLUME_NAME`.
- Dumps use PostgreSQL custom format (`.dump`), which is suitable for `pg_restore`.
- The script keeps the newest `POSTGRES_BACKUP_RETENTION_COUNT` dumps and deletes older ones automatically.
- A backup is created shortly after the backup container starts, then it repeats on the configured interval.

To inspect existing backups:

```bash
docker compose exec gym-tracker-db-backup ls -lh /backups
```

### Updating the stack

1. Pull the latest repo changes onto the server, or update the stack definition in Portainer.
2. Redeploy the stack in Portainer.
3. The API container applies EF Core migrations automatically during startup before serving requests.

### Legacy single-user data migration

This app now requires ownership on top-level records. Existing rows from the old single-user version are left in place and can be claimed once during deployment.

- If you already have data and want it assigned automatically, set `LEGACY_USER_EMAIL` and `LEGACY_USER_PASSWORD` before the first redeploy with this auth-enabled version.
- On startup, the API will create that account if needed and assign any ownerless legacy data to it.
- If those variables are left blank, legacy rows stay in the database but remain unclaimed and therefore invisible after login until you redeploy with those values set.
- New data created after this release is always written with the authenticated user ID and never trusts any frontend-supplied user identifier.

### Restoring from a backup

Choose the `.dump` file you want from `/backups`, then restore it with `pg_restore`.

Recommended restore flow:

1. Stop the API container so the app does not write while the restore is running:

```bash
docker compose stop gym-tracker-api
```

2. List available backups:

```bash
docker compose exec gym-tracker-db-backup ls -lh /backups
```

3. Restore the selected file:

```bash
docker compose exec -T gym-tracker-db-backup sh -lc 'export PGPASSWORD="$POSTGRES_PASSWORD"; pg_restore --clean --if-exists --no-owner --no-privileges --host "$PGHOST" --port "$PGPORT" --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" /backups/<backup-file>.dump'
```

4. Start the API again:

```bash
docker compose start gym-tracker-api
```

If you manage the stack through Portainer, the same sequence applies: stop `gym-tracker-api`, open a console in `gym-tracker-db-backup`, run the `pg_restore` command there, then start `gym-tracker-api` again.

### Health verification

- Frontend: open `APP_BASE_URL` in a browser.
- Backend health: `http://<server-or-domain>/api` is proxied through the web container, while backend container health is checked internally through `/healthz`.
- Database: Portainer should show `gym-tracker-db` as healthy after `pg_isready` succeeds.
- Auth check after deploy: register a user or log in, then confirm the dashboard loads and a second user account does not see the first user’s entries.

### Notes

- Only the web container is published to the host by default.
- The API and database stay on the internal Docker network and communicate by service name.
- The backup container also stays on the internal Docker network and does not publish host ports.
- `JWT_SIGNING_KEY` must be set to a long random secret before deployment. The API refuses to start with a missing or too-short signing key.
- This layout is suitable for a personal Linux server and can sit behind a reverse proxy or Cloudflare Tunnel later without changing app behavior.
