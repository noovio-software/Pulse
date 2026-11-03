#!/usr/bin/env bash
# Mise en place unique de FrontPulse sur le VPS, à lancer une fois depuis le dossier deploy/ copié sur le serveur :
#   scp -r FrontPulse/deploy ubuntu@<vps-host>:~/frontpulse-deploy
#   ssh ubuntu@<vps-host> 'cd ~/frontpulse-deploy && bash setup-server.sh'
# Rejouable sans risque : chaque étape vérifie ce qui existe déjà. Le DNS du domaine doit pointer sur le VPS (certbot).
set -euo pipefail

DOMAIN="${DOMAIN:-frontpulse.noovio.fr}"
APP_DIR=/home/ubuntu/projects/frontpulse
ENV_FILE=/etc/frontpulse/frontpulse.env

echo "== Dossiers"
mkdir -p "$APP_DIR/web" "$APP_DIR/worker"
sudo mkdir -p /etc/frontpulse /var/lib/frontpulse/keys
sudo chown ubuntu:ubuntu /var/lib/frontpulse/keys
sudo chmod 700 /var/lib/frontpulse/keys

echo "== PostgreSQL : base et utilisateur frontpulse"
if ! command -v psql >/dev/null; then
    sudo apt-get update && sudo apt-get install -y postgresql
fi
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'frontpulse'" | grep -q 1; then
    DB_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')"
    sudo -u postgres psql -c "CREATE ROLE frontpulse LOGIN PASSWORD '$DB_PASSWORD'"
    echo "   utilisateur créé (mot de passe écrit directement dans $ENV_FILE)"
else
    DB_PASSWORD=""
    echo "   utilisateur déjà présent"
fi
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname = 'frontpulse'" | grep -q 1; then
    # Les tables sont créées par les migrations, au premier démarrage du site ou du worker.
    sudo -u postgres createdb -O frontpulse frontpulse
fi

echo "== Fichier d'environnement $ENV_FILE"
if ! sudo test -f "$ENV_FILE"; then
    sudo cp frontpulse.env.example "$ENV_FILE"
    if [ -n "$DB_PASSWORD" ]; then
        sudo sed -i "s|Password=CHANGE_ME|Password=$DB_PASSWORD|" "$ENV_FILE"
    fi
    echo "   à compléter : Blizzard__ClientId et Blizzard__ClientSecret (sudo nano $ENV_FILE)"
fi
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

echo "== Unités systemd"
sudo cp kestrel-frontpulse.service kestrel-frontpulse-worker.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-frontpulse.service kestrel-frontpulse-worker.service

echo "== Nginx"
sed "s/frontpulse.noovio.fr/$DOMAIN/" nginx-frontpulse.conf | sudo tee /etc/nginx/sites-available/frontpulse >/dev/null
sudo ln -sf /etc/nginx/sites-available/frontpulse /etc/nginx/sites-enabled/frontpulse
sudo nginx -t
sudo systemctl reload nginx

echo "== Certificat Let's Encrypt"
sudo certbot --nginx -d "$DOMAIN" --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo
echo "Terminé. Reste à :"
echo "  1. compléter les clés Blizzard dans $ENV_FILE ;"
echo "  2. déclarer https://$DOMAIN/signin-battlenet dans le client Battle.net ;"
echo "  3. pousser un tag prod-frontpulse-x.y.z : le workflow déploie et démarre les deux services."
