#!/usr/bin/env bash
# Mise en place unique de LandingPagePulse sur le VPS, à lancer une fois depuis le dossier deploy/ copié sur le serveur :
#   scp -r LandingPagePulse/deploy ubuntu@<vps-host>:~/landingpagepulse-deploy
#   ssh ubuntu@<vps-host> 'cd ~/landingpagepulse-deploy && bash setup-server.sh'
# Rejouable sans risque. Ne touche pas à l'ancien site : la bascule de www.noovio.fr est faite par cutover-noovio.sh.
set -euo pipefail

APP_DIR=/home/ubuntu/projects/landingpagepulse
PORT=8520

echo "== Port $PORT"
if ss -ltn | grep -q "127.0.0.1:$PORT " && ! systemctl is-active --quiet kestrel-landingpagepulse.service; then
    echo "   le port $PORT est déjà pris par un autre service : à changer dans l'unité et le vhost." >&2
    exit 1
fi

echo "== Dossier"
mkdir -p "$APP_DIR"

echo "== Unité systemd"
sudo cp kestrel-landingpagepulse.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kestrel-landingpagepulse.service

echo "== Nginx : vhost préparé, pas encore activé"
# Une fois la bascule faite, certbot a complété ce fichier (blocs 443) : ne pas l'écraser.
if [ ! -e /etc/nginx/sites-enabled/landingpagepulse ]; then
    sudo cp nginx-landingpagepulse.conf /etc/nginx/sites-available/landingpagepulse
fi

echo
echo "Terminé. Reste à :"
echo "  1. pousser un tag prod-landingpagepulse-x.y.z : le workflow déploie et démarre le service ;"
echo "  2. lancer cutover-noovio.sh pour faire passer www.noovio.fr sur ce site et mettre l'ancien en veille."
