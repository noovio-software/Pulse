#!/usr/bin/env bash
# Mise en place unique de PulseMetrics Admin sur le VPS.
set -euo pipefail

DOMAIN="${DOMAIN:-admin.pulsemetrics.fr}"
APP_DIR=/home/ubuntu/projects/pulseadmin
ENV_FILE=/etc/pulseadmin/pulseadmin.env
PORT=8560

[ -f /etc/pulsemetrics/pulsemetrics.env ] || { echo "Installe d'abord PulseMetrics (PulseMetrics/deploy/setup-server.sh)." >&2; exit 1; }
CONNECTION="$(sudo grep -E '^ConnectionStrings__PulseUsage=' /etc/pulsemetrics/pulsemetrics.env | cut -d= -f2-)"
[ -n "$CONNECTION" ] || { echo "ConnectionStrings__PulseUsage absente de l'environnement PulseMetrics." >&2; exit 1; }

if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet kestrel-pulseadmin.service; then
    echo "Le port $PORT est déjà pris." >&2
    exit 1
fi

mkdir -p "$APP_DIR"
sudo mkdir -p /etc/pulseadmin /var/lib/pulseadmin/keys
sudo chown ubuntu:ubuntu /var/lib/pulseadmin/keys
sudo chmod 700 /var/lib/pulseadmin/keys

if ! sudo test -f "$ENV_FILE"; then
    sudo cp pulseadmin.env.example "$ENV_FILE"
fi
sudo sed -i '/^ConnectionStrings__PulseUsage=/d' "$ENV_FILE"
echo "ConnectionStrings__PulseUsage=$CONNECTION" | sudo tee -a "$ENV_FILE" >/dev/null
# Jeton du bot Discord et clés des notifications : générés une fois, jamais remplacés.
if ! sudo grep -qE '^Api__Key=.+' "$ENV_FILE"; then
    sudo sed -i '/^Api__Key=/d' "$ENV_FILE"
    echo "Api__Key=$(openssl rand -hex 32)" | sudo tee -a "$ENV_FILE" >/dev/null
fi
if ! sudo grep -qE '^Notifications__PrivateKey=.+' "$ENV_FILE" && [ -x "$APP_DIR/PulseAdmin" ]; then
    sudo sed -i '/^Notifications__PublicKey=/d; /^Notifications__PrivateKey=/d' "$ENV_FILE"
    "$APP_DIR/PulseAdmin" --vapid-keys | sudo tee -a "$ENV_FILE" >/dev/null
fi
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

sudo cp kestrel-pulseadmin.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-pulseadmin.service

sed "s/admin.pulsemetrics.fr/$DOMAIN/" nginx-pulseadmin.conf | sudo tee /etc/nginx/sites-available/pulseadmin >/dev/null
sudo ln -sf /etc/nginx/sites-available/pulseadmin /etc/nginx/sites-enabled/pulseadmin
sudo nginx -t
sudo systemctl reload nginx
sudo certbot --nginx -d "$DOMAIN" --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo "Terminé. Compléter les clés Blizzard et le BattleTag dans $ENV_FILE."
echo "Déclarer https://$DOMAIN/signin-battlenet dans le client Battle.net."
echo "Déployer avec un tag prod-admin-x.y.z."
echo "Clés des notifications absentes au premier passage (pas encore d'exécutable) : relancer ce script après le premier déploiement."
echo "Jeton du bot : sudo grep Api__Key $ENV_FILE (à recopier en PULSEADMIN_API_KEY dans /etc/nooviobot/nooviobot.env)."