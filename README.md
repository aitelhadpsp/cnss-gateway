# CnssProxy

A lightweight .NET 8 reverse proxy for `sandboxfse-dev.cnss.ma`.

All requests to `/cnss/**` are forwarded upstream using a clean HttpClient
with fixed headers — no client headers are forwarded to CNSS.

## Usage

Any request you previously sent to:
```
https://sandboxfse-dev.cnss.ma/api/v1/fse/...
```
Now becomes:
```
https://cnss.dentalevo.net/cnss/api/v1/fse/...
```

## Run locally

```bash
dotnet run
# Listening on http://localhost:5050
```

## Run with Docker

```bash
docker-compose up -d
```

## Deploy behind Nginx

Add this to your nginx config for `cnss.dentalevo.net`:

```nginx
location / {
    proxy_pass http://localhost:5050;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header Connection "";
    proxy_read_timeout 120s;
}
```

Then point your SSL server block to this .NET app instead of directly to CNSS.
# cnss-gateway
