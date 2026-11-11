#!/usr/bin/env bash
# Mise en place unique de BurstPulse sur le VPS, à lancer une fois depuis le dossier deploy/ copié sur le serveur :
#   scp -r BurstPulse/deploy ubuntu@<vps-host>:~/burstpulse-deploy
#   ssh ubuntu@<vps-host> 'cd ~/burstpulse-deploy && bash setup-server.sh'
# Rejouable sans risque : chaque étape vérifie ce qui existe déjà. Le DNS du domaine doit pointer sur le VPS (certbot).
set -euo pipefail

DOMAIN="${DOMAIN:-burstpulse.noovio.fr}"
APP_DIR=/home/ubuntu/projects/burstpulse
ENV_FILE=/etc/burstpulse/burstpulse.env

echo "== Dossiers"
mkdir -p "$APP_DIR/web" "$APP_DIR/worker"
sudo mkdir -p /etc/burstpulse /var/lib/burstpulse/keys
sudo chown ubuntu:ubuntu /var/lib/burstpulse/keys
sudo chmod 700 /var/lib/burstpulse/keys

echo "== PostgreSQL : base et utilisateur burstpulse"
if ! command -v psql >/dev/null; then
    sudo apt-get update && sudo apt-get install -y postgresql
fi
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'burstpulse'" | grep -q 1; then
    DB_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')"
    sudo -u postgres psql -c "CREATE ROLE burstpulse LOGIN PASSWORD '$DB_PASSWORD'"
    echo "   utilisateur créé (mot de passe écrit directement dans $ENV_FILE)"
else
    DB_PASSWORD=""
    echo "   utilisateur déjà présent"
fi
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname = 'burstpulse'" | grep -q 1; then
    # Les tables sont créées par les migrations, au premier démarrage du site ou du worker.
    sudo -u postgres createdb -O burstpulse burstpulse
fi

echo "== Fichier d'environnement $ENV_FILE"
if ! sudo test -f "$ENV_FILE"; then
    sudo cp burstpulse.env.example "$ENV_FILE"
    if [ -n "$DB_PASSWORD" ]; then
        sudo sed -i "s|Password=CHANGE_ME|Password=$DB_PASSWORD|" "$ENV_FILE"
    fi
    echo "   à compléter : Blizzard__ClientId et Blizzard__ClientSecret (sudo nano $ENV_FILE)"
fi
if ! sudo grep -qE '^Admin__ApiKey=.{32,}' "$ENV_FILE"; then
    # Clé d'admin pour l'ingestion forcée en ligne de commande, jamais affichée : sudo grep Admin__ApiKey "$ENV_FILE"
    sudo sed -i '/^Admin__ApiKey=/d' "$ENV_FILE"
    echo "Admin__ApiKey=$(openssl rand -hex 24)" | sudo tee -a "$ENV_FILE" >/dev/null
fi
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

echo "== Unités systemd"
sudo cp kestrel-burstpulse.service kestrel-burstpulse-worker.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-burstpulse.service kestrel-burstpulse-worker.service

echo "== Nginx"
sed "s/burstpulse.noovio.fr/$DOMAIN/" nginx-burstpulse.conf | sudo tee /etc/nginx/sites-available/burstpulse >/dev/null
sudo ln -sf /etc/nginx/sites-available/burstpulse /etc/nginx/sites-enabled/burstpulse
sudo nginx -t
sudo systemctl reload nginx

echo "== Certificat Let's Encrypt"
sudo certbot --nginx -d "$DOMAIN" --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo
echo "Terminé. Reste à :"
echo "  1. compléter les clés Blizzard dans $ENV_FILE ;"
echo "  2. déclarer https://$DOMAIN/signin-battlenet dans le client Battle.net ;"
echo "  3. pousser un tag prod-burstpulse-x.y.z : le workflow déploie et démarre les deux services."
