#!/usr/bin/env bash
# Bascule des anciens domaines, à lancer une fois, juste après le premier déploiement de PulseMetrics :
#   altpulse / frontpulse / lifepulse / burstpulse.noovio.fr et (www.)noovio.fr  →  https://pulsemetrics.fr/…
# Désactive les anciens vhosts (gardés dans sites-available, pour revenir en arrière) et les anciennes unités systemd.
# Rejouable sans risque.
set -euo pipefail

OLD_VHOSTS=(altpulse frontpulse lifepulse burstpulse landingpagepulse)
OLD_UNITS=(kestrel-altpulse kestrel-frontpulse kestrel-frontpulse-worker kestrel-lifepulse kestrel-lifepulse-worker
           kestrel-burstpulse kestrel-burstpulse-worker kestrel-landingpagepulse)

systemctl is-active --quiet kestrel-pulsemetrics.service \
    || { echo "kestrel-pulsemetrics n'est pas démarré : déploie d'abord (tag prod-pulsemetrics-x.y.z)." >&2; exit 1; }

echo "== Anciens vhosts"
for vhost in "${OLD_VHOSTS[@]}"; do
    if [ -e "/etc/nginx/sites-enabled/$vhost" ]; then
        sudo rm "/etc/nginx/sites-enabled/$vhost"
        echo "   $vhost désactivé (toujours dans sites-available)"
    fi
done

echo "== Redirections"
if [ ! -e /etc/nginx/sites-enabled/pulsemetrics-legacy ]; then
    sudo cp nginx-legacy.conf /etc/nginx/sites-available/pulsemetrics-legacy
    sudo ln -sf /etc/nginx/sites-available/pulsemetrics-legacy /etc/nginx/sites-enabled/pulsemetrics-legacy
fi
sudo nginx -t
sudo systemctl reload nginx

# Les anciens liens sont souvent en HTTPS : certbot ajoute les blocs 443 aux redirections.
sudo certbot --nginx --cert-name pulsemetrics-legacy \
    -d altpulse.noovio.fr -d frontpulse.noovio.fr -d lifepulse.noovio.fr -d burstpulse.noovio.fr \
    -d noovio.fr -d www.noovio.fr \
    --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo "== Anciennes unités systemd"
for unit in "${OLD_UNITS[@]}"; do
    if systemctl list-unit-files "$unit.service" --no-legend | grep -q "$unit"; then
        sudo systemctl disable --now "$unit.service"
        echo "   $unit arrêté et désactivé"
    fi
done

echo
echo "Terminé. Anciennes bases conservées : PulseMetrics les utilise (voir /etc/pulsemetrics/pulsemetrics.env)."
echo "Une fois tout vérifié : sudo rm -r /etc/{altpulse,frontpulse,lifepulse,burstpulse} /home/ubuntu/projects/{altpulse,frontpulse,lifepulse,burstpulse,landingpagepulse}"
