# Gym Tracker

## Portainer Deployment

This repo includes a Portainer-ready stack in [`docker-compose.yml`](./docker-compose.yml).

### Required variables

Set these values in Portainer when deploying the stack, or place them in a local `.env` file before running `docker compose`:

- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_VOLUME_NAME`
- `APP_BASE_URL`
- `WEB_PORT`

Example values are provided in [`/.env.example`](./.env.example).

### Deploy in Portainer

1. Open `Stacks` in Portainer.
2. Create a new stack.
3. Paste the contents of [`docker-compose.yml`](./docker-compose.yml).
4. Add the environment variables from [`/.env.example`](./.env.example) and replace the example password and URL values.
5. Deploy the stack.

### Persistence

PostgreSQL data is stored in the Docker volume named by `POSTGRES_VOLUME_NAME`.

The default is `gym-tracker_gym-tracker-postgres-data`, which matches the volume name created by the current stack name and prevents redeploys from silently switching to a fresh empty volume when the Compose project name changes.

Recreating containers will not remove database data unless that volume is explicitly deleted.

### Updating the stack

1. Pull the latest repo changes onto the server, or update the stack definition in Portainer.
2. Redeploy the stack in Portainer.
3. The API container applies EF Core migrations automatically during startup before serving requests.

### Health verification

- Frontend: open `APP_BASE_URL` in a browser.
- Backend health: `http://<server-or-domain>/api` is proxied through the web container, while backend container health is checked internally through `/healthz`.
- Database: Portainer should show `gym-tracker-db` as healthy after `pg_isready` succeeds.

### Notes

- Only the web container is published to the host by default.
- The API and database stay on the internal Docker network and communicate by service name.
- This layout is suitable for a personal Linux server and can sit behind a reverse proxy or Cloudflare Tunnel later without changing app behavior.
