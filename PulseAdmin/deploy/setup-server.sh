#!/usr/bin/env bash
# Mise en place unique de PulseAdmin sur le VPS, à lancer une fois depuis le dossier deploy/ copié sur le serveur :
#   scp -r PulseAdmin/deploy ubuntu@<vps-host>:~/pulseadmin-deploy
#   ssh ubuntu@<vps-host> 'cd ~/pulseadmin-deploy && bash setup-server.sh'
# Rejouable sans risque : chaque étape vérifie ce qui existe déjà. Le DNS du domaine doit pointer sur le VPS (certbot).
# Crée aussi la base d'usage commune et ajoute sa chaîne de connexion aux fichiers d'environnement des trois sites.
set -euo pipefail

DOMAIN="${DOMAIN:-pulseadmin.noovio.fr}"
APP_DIR=/home/ubuntu/projects/pulseadmin
ENV_FILE=/etc/pulseadmin/pulseadmin.env
PORT=8560

echo "== Port $PORT"
if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet kestrel-pulseadmin.service; then
    echo "   le port $PORT est déjà pris par un autre service : à changer dans l'unité et le vhost." >&2
    exit 1
fi

echo "== Dossiers"
mkdir -p "$APP_DIR"
sudo mkdir -p /etc/pulseadmin /var/lib/pulseadmin/keys
sudo chown ubuntu:ubuntu /var/lib/pulseadmin/keys
sudo chmod 700 /var/lib/pulseadmin/keys

echo "== PostgreSQL : base et utilisateur pulseusage"
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname = 'pulseusage'" | grep -q 1; then
    DB_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=')"
    sudo -u postgres psql -c "CREATE ROLE pulseusage LOGIN PASSWORD '$DB_PASSWORD'"
    echo "   utilisateur créé (mot de passe écrit directement dans les fichiers d'environnement)"
else
    DB_PASSWORD=""
    echo "   utilisateur déjà présent"
fi
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname = 'pulseusage'" | grep -q 1; then
    # La table est créée par les migrations, au premier démarrage de PulseAdmin ou d'un des sites.
    sudo -u postgres createdb -O pulseusage pulseusage
fi
CONNECTION="Host=localhost;Port=5432;Database=pulseusage;Username=pulseusage;Password=$DB_PASSWORD"

echo "== Fichier d'environnement $ENV_FILE"
if ! sudo test -f "$ENV_FILE"; then
    sudo cp pulseadmin.env.example "$ENV_FILE"
    if [ -n "$DB_PASSWORD" ]; then
        sudo sed -i "s|^ConnectionStrings__PulseUsage=.*|ConnectionStrings__PulseUsage=$CONNECTION|" "$ENV_FILE"
    fi
    echo "   à compléter : Blizzard__ClientId, Blizzard__ClientSecret, Admin__BattleTags__0 (sudo nano $ENV_FILE)"
fi
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

echo "== Base d'usage dans les sites"
# Le mot de passe n'est connu qu'à la création du rôle : sinon on reprend la chaîne déjà écrite pour PulseAdmin.
if [ -z "$DB_PASSWORD" ]; then
    CONNECTION="$(sudo grep -E '^ConnectionStrings__PulseUsage=' "$ENV_FILE" | cut -d= -f2-)"
fi
for app in frontpulse lifepulse burstpulse; do
    file="/etc/$app/$app.env"
    if ! sudo test -f "$file"; then
        echo "   $app : pas de $file, ignoré"
    elif sudo grep -qE '^ConnectionStrings__PulseUsage=' "$file"; then
        echo "   $app : déjà relié"
    elif [[ "$CONNECTION" == *CHANGE_ME* || "$CONNECTION" == *"Password=" ]]; then
        echo "   $app : chaîne inconnue, à ajouter à la main (ConnectionStrings__PulseUsage)"
    else
        echo "ConnectionStrings__PulseUsage=$CONNECTION" | sudo tee -a "$file" >/dev/null
        echo "   $app : relié (redémarrer kestrel-$app.service pour l'appliquer)"
    fi
done

echo "== Unité systemd"
sudo cp kestrel-pulseadmin.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-pulseadmin.service

echo "== Nginx"
sed "s/pulseadmin.noovio.fr/$DOMAIN/" nginx-pulseadmin.conf | sudo tee /etc/nginx/sites-available/pulseadmin >/dev/null
sudo ln -sf /etc/nginx/sites-available/pulseadmin /etc/nginx/sites-enabled/pulseadmin
sudo nginx -t
sudo systemctl reload nginx

echo "== Certificat Let's Encrypt"
sudo certbot --nginx -d "$DOMAIN" --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo
echo "Terminé. Reste à :"
echo "  1. compléter les clés Blizzard et ton BattleTag dans $ENV_FILE ;"
echo "  2. déclarer https://$DOMAIN/signin-battlenet dans le client Battle.net ;"
echo "  3. redémarrer les trois sites (sudo systemctl restart kestrel-frontpulse kestrel-lifepulse kestrel-burstpulse) ;"
echo "  4. pousser un tag prod-pulseadmin-x.y.z : le workflow déploie et démarre le service."
