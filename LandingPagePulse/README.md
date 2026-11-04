# LandingPagePulse by Noovio

Presentation page of the Noovio brand at https://www.noovio.fr: it introduces AltPulse and FrontPulse and sends visitors to them.

- **Stack**: ASP.NET Core / Blazor Web App .NET 10, static server rendering only (no circuit, no WebAssembly, no JavaScript, no cookie, no database).
- **Redirects**: `/altpulse` and `/frontpulse` answer a 302 to the tool. The targets live in `appsettings.json` (`Tools` section), and every link on the page goes through them.
- **Theme**: « Battement », the page is shared between the two tools' worlds: AltPulse's venom green on the left, FrontPulse's forge amber on the right. The side of the tool being hovered lights up.

## Run it

```powershell
dotnet run --project LandingPagePulse/LandingPagePulse --launch-profile http
# → http://localhost:5188
```

In development, the redirects target the tools' local ports (5166 and 5177).

## Production (https://www.noovio.fr)

Hosted on the same OVH VPS as AltPulse and FrontPulse.

| Piece | Location |
|---|---|
| App files | `/home/ubuntu/projects/landingpagepulse/` |
| systemd unit | `/etc/systemd/system/kestrel-landingpagepulse.service` (Kestrel on `127.0.0.1:8520`) |
| Nginx vhost | `/etc/nginx/sites-available/landingpagepulse` (`noovio.fr` → `www.noovio.fr`, HTTP → HTTPS) |
| TLS certificate | Let's Encrypt `www.noovio.fr` + `noovio.fr`, renewed by certbot |

### First install and switch from the previous site

The previous Noovio site currently serves `noovio.fr` and `www.noovio.fr` on this VPS. It goes on standby during the switch: its vhost is disabled and its services are stopped, but its files, database and certificate stay in place.

1. Add the GitHub secret `REMOTE_TARGET_LANDINGPAGEPULSE` = `/home/ubuntu/projects/landingpagepulse/` (the other secrets are shared with AltPulse).
2. Prepare the server, without touching the previous site:
   ```bash
   scp -r LandingPagePulse/deploy ubuntu@<vps-host>:~/landingpagepulse-deploy
   ssh ubuntu@<vps-host> 'cd ~/landingpagepulse-deploy && bash setup-server.sh'
   ```
3. Deploy the site (see below). It then answers on `127.0.0.1:8520` only.
4. On the VPS, from `~/landingpagepulse-deploy`, switch the domain:
   ```bash
   bash cutover-noovio.sh                          # shows the previous site's vhost and services, changes nothing
   bash cutover-noovio.sh <vhost> <unit> [<unit>…] # switches, and goes back to the previous site if a step fails
   ```

To put the previous site back online: `bash cutover-noovio.sh --rollback <vhost> <unit> [<unit>…]`.

The script only stops the units it is given: cron jobs or other background processes of the previous site need handling separately.

### Deploy (GitHub Actions)

The workflow `.github/workflows/landingpagepulse.yml` publishes, rsyncs, restarts the service and smoke-tests the site on a tag:

```powershell
git tag prod-landingpagepulse-1.0.0
git push origin prod-landingpagepulse-1.0.0
```
