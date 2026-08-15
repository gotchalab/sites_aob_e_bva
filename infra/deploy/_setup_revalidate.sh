#!/usr/bin/env bash
# Configura a revalidação automática do Next.js no VPS.
# Corre uma vez; idempotente (não duplica vars existentes).
#
# Uso:
#   ADMIN_API_SECRET=<secret1> API_FRONTEND_SECRET=<secret2> ./infra/deploy/_setup_revalidate.sh
#
# Ou exporta primeiro:
#   export ADMIN_API_SECRET=... API_FRONTEND_SECRET=...
#   ./infra/deploy/_setup_revalidate.sh
set -euo pipefail

SSH_USER=${AOB_SSH_USER:-debian}
SSH_HOST=${AOB_SSH_HOST:?Defina AOB_SSH_HOST com IP/hostname do VPS}
SSH_TARGET="$SSH_USER@$SSH_HOST"

ADMIN_API_SECRET=${ADMIN_API_SECRET:?Defina ADMIN_API_SECRET}
API_FRONTEND_SECRET=${API_FRONTEND_SECRET:?Defina API_FRONTEND_SECRET}

# Adiciona uma var ao ficheiro se ainda não existir
# Uso: add_var /etc/aob/foo.env CHAVE valor
add_var() {
    local file=$1 key=$2 value=$3
    ssh "$SSH_TARGET" "
        sudo touch '$file'
        if ! sudo grep -q '^${key}=' '$file' 2>/dev/null; then
            echo '${key}=${value}' | sudo tee -a '$file' > /dev/null
            echo '  + adicionado ${key}'
        else
            echo '  = já existe ${key} (sem alteração)'
        fi
    "
}

echo "==> Configurar /etc/aob/admin.env"
add_var /etc/aob/admin.env Api__BaseUrl        "http://127.0.0.1:5000"
add_var /etc/aob/admin.env Revalidate__Secret  "$ADMIN_API_SECRET"

echo "==> Configurar /etc/aob/api.env"
add_var /etc/aob/api.env Revalidate__Secret     "$ADMIN_API_SECRET"
add_var /etc/aob/api.env Revalidate__AobUrl     "http://127.0.0.1:3000"
add_var /etc/aob/api.env Revalidate__BvaUrl     "http://127.0.0.1:3001"
add_var /etc/aob/api.env Revalidate__AobSecret  "$API_FRONTEND_SECRET"
add_var /etc/aob/api.env Revalidate__BvaSecret  "$API_FRONTEND_SECRET"

echo "==> Configurar /etc/aob/aobarcelos.env"
add_var /etc/aob/aobarcelos.env REVALIDATE_SECRET "$API_FRONTEND_SECRET"

echo "==> Configurar /etc/aob/bva-portugal.env"
add_var /etc/aob/bva-portugal.env REVALIDATE_SECRET "$API_FRONTEND_SECRET"

echo "==> Reiniciar serviços"
ssh "$SSH_TARGET" "
    sudo systemctl restart aob-admin aob-api aob-aobarcelos aob-bva-portugal
    echo '--- estado dos serviços ---'
    for s in aob-admin aob-api aob-aobarcelos aob-bva-portugal; do
        sudo systemctl is-active \$s && echo \"\$s: OK\" || echo \"\$s: FALHOU\"
    done
"

echo "==> Revalidação configurada com sucesso."
echo ""
echo "Para testar manualmente:"
echo "  curl -X POST 'https://aobarcelos.pt/api/revalidate?path=/artigos&secret=${API_FRONTEND_SECRET}'"
