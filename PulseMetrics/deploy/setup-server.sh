#!/usr/bin/env bash
# Mise en place unique de PulseMetrics sur le VPS, depuis le dossier deploy/ copié sur le serveur :
#   scp -r PulseMetrics/deploy ubuntu@<vps-host>:~/pulsemetrics-deploy
#   ssh ubuntu@<vps-host> 'cd ~/pulsemetrics-deploy && bash setup-server.sh'
# Rejouable sans risque : chaque étape vérifie ce qui existe déjà. Le DNS de pulsemetrics.fr doit pointer sur le VPS.
# Ne touche ni aux anciens services ni aux anciens domaines : voir cutover-legacy.sh, à lancer après le premier déploiement.
set -euo pipefail

DOMAIN="${DOMAIN:-pulsemetrics.fr}"
APP_DIR=/home/ubuntu/projects/pulsemetrics
ENV_FILE=/etc/pulsemetrics/pulsemetrics.env
PORT=8580

echo "== Port $PORT"
if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet kestrel-pulsemetrics.service; then
    echo "   le port $PORT est déjà pris par un autre service : à changer dans appsettings.json (Urls) et le vhost." >&2
    exit 1
fi

echo "== Dossiers"
mkdir -p "$APP_DIR"
sudo mkdir -p /etc/pulsemetrics /var/lib/pulsemetrics/keys
sudo chown ubuntu:ubuntu /var/lib/pulsemetrics/keys
sudo chmod 700 /var/lib/pulsemetrics/keys

# Valeur d'une clé dans un ancien fichier d'environnement, vide si absente.
old_value() {
    sudo test -f "$1" && sudo grep -E "^$2=" "$1" | tail -n 1 | cut -d= -f2- || true
}

# Remplace la ligne « clé=… » (ou « #clé=… ») d'un fichier. En bash pur : les valeurs (mots de passe, chaînes de
# connexion) peuvent contenir ; & / \ que sed interpréterait.
set_value() {
    local file="$1" key="$2" value="$3" out line
    out="$(mktemp)"
    while IFS= read -r line || [ -n "$line" ]; do
        if [[ "$line" == "$key="* || "$line" == "#$key="* ]]; then
            printf '%s\n' "$key=$value"
        else
            printf '%s\n' "$line"
        fi
    done < "$file" > "$out"
    mv "$out" "$file"
}

echo "== Fichier d'environnement $ENV_FILE"
if sudo test -f "$ENV_FILE"; then
    echo "   déjà présent, laissé tel quel"
else
    tmp="$(mktemp)"
    cp pulsemetrics.env.example "$tmp"
    # Sur le VPS existant : reprend les bases et les secrets des anciens sites, sans copie de données.
    front=/etc/frontpulse/frontpulse.env
    declare -A sources=(
        [ConnectionStrings__FrontPulse]="$front"
        [ConnectionStrings__LifePulse]=/etc/lifepulse/lifepulse.env
        [ConnectionStrings__BurstPulse]=/etc/burstpulse/burstpulse.env
        [ConnectionStrings__PulseUsage]="$front"
        [ConnectionStrings__Redis]="$front"
        [Blizzard__ClientId]="$front"
        [Blizzard__ClientSecret]="$front"
        [KeystoneGuru__Email]="$front"
        [KeystoneGuru__Password]="$front"
        [Admin__ApiKey]="$front"
    )
    for key in "${!sources[@]}"; do
        value="$(old_value "${sources[$key]}" "$key")"
        if [ -n "$value" ]; then
            set_value "$tmp" "$key" "$value"
            echo "   $key repris de ${sources[$key]}"
        fi
    done
    sudo grep -hE '^Admin__BattleTags__[0-9]+=' "$front" 2>/dev/null >> "$tmp" || true
    if [ -z "$(old_value "$front" Admin__ApiKey)" ]; then
        set_value "$tmp" Admin__ApiKey "$(openssl rand -hex 32)"
    fi
    sudo install -o root -g root -m 600 "$tmp" "$ENV_FILE"
    rm -f "$tmp"
    if sudo grep -q CHANGE_ME "$ENV_FILE"; then
        echo "   à compléter (sudo nano $ENV_FILE) :"
        sudo grep -n CHANGE_ME "$ENV_FILE" | cut -d= -f1 | sed 's/^/     /'
    fi
fi
sudo chown root:root "$ENV_FILE"
sudo chmod 600 "$ENV_FILE"

echo "== Unité systemd"
sudo cp kestrel-pulsemetrics.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-pulsemetrics.service

echo "== Nginx"
# Une fois certbot passé, le fichier contient les blocs 443 : ne pas l'écraser.
if [ ! -e /etc/nginx/sites-enabled/pulsemetrics ]; then
    sed "s/pulsemetrics\.fr/$DOMAIN/g" nginx-pulsemetrics.conf | sudo tee /etc/nginx/sites-available/pulsemetrics >/dev/null
    sudo ln -sf /etc/nginx/sites-available/pulsemetrics /etc/nginx/sites-enabled/pulsemetrics
fi
sudo nginx -t
sudo systemctl reload nginx

echo "== Certificat Let's Encrypt"
sudo certbot --nginx -d "$DOMAIN" -d "www.$DOMAIN" --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring

echo
echo "Terminé. Reste à :"
echo "  1. vérifier $ENV_FILE (sudo nano $ENV_FILE) ;"
echo "  2. déclarer https://$DOMAIN/{front,life,burst}/signin-battlenet dans le client Battle.net ;"
echo "  3. pousser un tag prod-pulsemetrics-x.y.z : le workflow déploie et démarre ce service (les anciens tournent encore) ;"
echo "  4. lancer cutover-legacy.sh : les anciens domaines redirigent vers $DOMAIN."
