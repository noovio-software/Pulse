#!/usr/bin/env bash
# Fait passer noovio.fr et www.noovio.fr sur LandingPagePulse et met l'ancien site en veille. À lancer sur le VPS, depuis ce dossier.
#
#   bash cutover-noovio.sh                                  → montre l'ancien site (vhost, services), ne change rien
#   bash cutover-noovio.sh <vhost> <unité> [<unité>...]     → bascule
#   bash cutover-noovio.sh --rollback <vhost> <unité> [...] → remet l'ancien site en ligne
#
# Veille = vhost désactivé et services arrêtés. Rien n'est supprimé : fichiers, base de données et certificat restent en place.
set -euo pipefail

NEW_VHOST=landingpagepulse
NEW_PORT=8520

reload_nginx() { sudo nginx -t && sudo systemctl reload nginx; }

restore_old_site() {
    local vhost="$1"; shift
    sudo rm -f "/etc/nginx/sites-enabled/$NEW_VHOST"
    sudo ln -sf "/etc/nginx/sites-available/$vhost" "/etc/nginx/sites-enabled/$vhost"
    reload_nginx
    sudo systemctl enable --now "$@"
}

if [ "${1:-}" = "--rollback" ]; then
    shift
    [ $# -ge 2 ] || { echo "Usage : $0 --rollback <vhost> <unité> [<unité>...]" >&2; exit 2; }
    restore_old_site "$@"
    echo "Ancien site remis en ligne."
    exit 0
fi

if [ $# -eq 0 ]; then
    echo "== Vhosts actifs qui servent noovio.fr ou www.noovio.fr"
    for file in /etc/nginx/sites-enabled/*; do
        if sudo grep -qE 'server_name[^;]*[[:space:]](www\.)?noovio\.fr[[:space:];]' "$file"; then
            echo "--- $(basename "$file")"
            sudo grep -E '^[[:space:]]*(server_name|proxy_pass|root)[[:space:]]' "$file" | sort -u
        fi
    done
    echo
    echo "== Services Kestrel"
    systemctl list-units --type=service --no-pager --plain 'kestrel-*' | grep -E '^kestrel-' || true
    echo
    echo "Puis : bash $0 <vhost> <unité> [<unité>...]"
    exit 0
fi

[ $# -ge 2 ] || { echo "Usage : $0 <vhost> <unité> [<unité>...]" >&2; exit 2; }
OLD_VHOST="$1"
shift

echo "== Vérifications"
[ -e "/etc/nginx/sites-enabled/$OLD_VHOST" ] || { echo "Vhost actif introuvable : $OLD_VHOST" >&2; exit 1; }
[ -e "/etc/nginx/sites-available/$NEW_VHOST" ] || { echo "Lance d'abord setup-server.sh." >&2; exit 1; }
curl -fsS -o /dev/null "http://127.0.0.1:$NEW_PORT/" || { echo "LandingPagePulse ne répond pas sur le port $NEW_PORT : déploie-le d'abord." >&2; exit 1; }
# Le vhost va être désactivé en entier : il ne doit servir aucun autre domaine.
OTHER_NAMES="$(sudo grep -hE '^[[:space:]]*server_name' "/etc/nginx/sites-enabled/$OLD_VHOST" | sed 's/server_name//;s/;//' | tr -s ' \t' '\n' | grep -vE '^(www\.)?noovio\.fr$|^$' | sort -u || true)"
if [ -n "$OTHER_NAMES" ]; then
    echo "Ce vhost sert aussi d'autres domaines, qui tomberaient avec lui :" >&2
    echo "$OTHER_NAMES" >&2
    exit 1
fi

# Toute erreur à partir d'ici remet l'ancien site en ligne.
trap 'echo "Échec : retour au site précédent."; restore_old_site "$OLD_VHOST" "$@"' ERR

echo "== Nginx : $OLD_VHOST désactivé, $NEW_VHOST activé"
sudo rm "/etc/nginx/sites-enabled/$OLD_VHOST"
sudo ln -sf "/etc/nginx/sites-available/$NEW_VHOST" "/etc/nginx/sites-enabled/$NEW_VHOST"
reload_nginx

echo "== Certificat Let's Encrypt"
sudo certbot --nginx -d www.noovio.fr -d noovio.fr --redirect --non-interactive --agree-tos --register-unsafely-without-email --keep-until-expiring
reload_nginx

echo "== Vérification publique"
# La cible de /altpulse n'existe que sur le nouveau site : un 302 prouve que c'est bien lui qui répond.
sleep 2
[ "$(curl -sS -o /dev/null -w '%{http_code}' https://www.noovio.fr/altpulse)" = "302" ]
[ "$(curl -sS -o /dev/null -w '%{redirect_url}' https://noovio.fr/)" = "https://www.noovio.fr/" ]

trap - ERR

echo "== Ancien site en veille : $*"
sudo systemctl disable --now "$@"

echo
echo "Terminé : https://www.noovio.fr sert LandingPagePulse."
echo "Retour arrière : bash $0 --rollback $OLD_VHOST $*"
