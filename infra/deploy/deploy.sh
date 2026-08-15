#!/usr/bin/env bash
# Deploy do local para VPS via rsync + restart systemd.
# Uso: ./deploy.sh [api|admin|aobarcelos|bva|infra|install-next|all]
#
# Pre-requisito Next.js (uma so vez no VPS):
#   ./deploy.sh install-next
#   Instala next@15.5.4 globalmente em /usr/bin/next.
#
# Variaveis de ambiente obrigatorias:
#   AOB_SSH_HOST  — IP ou hostname do VPS
# Opcionais:
#   AOB_SSH_USER  — utilizador SSH (default: debian)
#
# Build Next.js le NEXT_PUBLIC_* do ambiente local.
# Definir antes de correr (ex: export NEXT_PUBLIC_API_URL=https://...).
set -euo pipefail

TARGET=${1:-all}
SSH_USER=${AOB_SSH_USER:-debian}
SSH_HOST=${AOB_SSH_HOST:?Defina AOB_SSH_HOST com IP/hostname do VPS}
SSH_TARGET="$SSH_USER@$SSH_HOST"

REPO_ROOT=$(cd "$(dirname "$0")/../.." && pwd)
BACKEND=$REPO_ROOT/backend
FRONTENDS=$REPO_ROOT/frontends

NEXT_VERSION="15.5.4"

deploy_dotnet() {
    local name=$1 project=$2 remote_dir=$3 service=$4
    echo "==> Build $name (.NET)"
    local out
    out=$(mktemp -d)
    dotnet publish "$BACKEND/src/$project/$project.csproj" \
        -c Release -o "$out" \
        --nologo --self-contained false -p:PublishSingleFile=false
    echo "==> Rsync $name → $remote_dir"
    rsync -az --delete "$out/" "$SSH_TARGET:$remote_dir/"
    echo "==> Restart $service"
    ssh "$SSH_TARGET" "sudo systemctl restart $service && sudo systemctl status --no-pager $service | head -5"
    rm -rf "$out"
}

deploy_next() {
    local name=$1 dir=$2 remote_dir=$3 service=$4
    echo "==> Build $name (Next.js $NEXT_VERSION)"
    # Build corre localmente — NEXT_PUBLIC_* tem de estar no ambiente do chamador
    (cd "$FRONTENDS/$dir" && npx next build)

    echo "==> Rsync $name → $remote_dir"
    # Envia apenas output compilado; node_modules NAO vai para o VPS.
    # O VPS corre "next start" com o next instalado globalmente (/usr/bin/next).
    ssh "$SSH_TARGET" "sudo mkdir -p $remote_dir && sudo chown $SSH_USER:$SSH_USER $remote_dir"

    rsync -az --delete \
        --exclude='.next/cache' \
        "$FRONTENDS/$dir/.next/" "$SSH_TARGET:$remote_dir/.next/"

    rsync -az "$FRONTENDS/$dir/package.json"    "$SSH_TARGET:$remote_dir/package.json"
    rsync -az "$FRONTENDS/$dir/next.config.mjs" "$SSH_TARGET:$remote_dir/next.config.mjs"

    if [[ -d "$FRONTENDS/$dir/public" ]]; then
        rsync -az --delete "$FRONTENDS/$dir/public/" "$SSH_TARGET:$remote_dir/public/"
    fi

    echo "==> Chown + restart $service"
    ssh "$SSH_TARGET" "
        sudo chown -R aob-web:aob-web $remote_dir
        sudo systemctl restart $service
        sudo systemctl status --no-pager $service | head -5
    "
}

deploy_infra() {
    echo "==> Rsync infra (nginx + systemd)"
    ssh "$SSH_TARGET" "sudo mkdir -p /opt/aob/infra && sudo chown $SSH_USER:$SSH_USER /opt/aob/infra"
    rsync -az --delete "$REPO_ROOT/infra/" "$SSH_TARGET:/opt/aob/infra/"
    ssh "$SSH_TARGET" '
        sudo cp /opt/aob/infra/systemd/*.service /etc/systemd/system/
        # Sites (vhosts)
        for f in /opt/aob/infra/nginx/*.pt.conf; do
            sudo cp "$f" /etc/nginx/sites-available/
        done
        # Snippet partilhado (contexto server)
        sudo mkdir -p /etc/nginx/snippets
        [[ -f /opt/aob/infra/nginx/_common.conf ]] && \
            sudo cp /opt/aob/infra/nginx/_common.conf /etc/nginx/snippets/aob-common.conf
        # Zonas de rate limit (contexto http)
        sudo mkdir -p /etc/nginx/conf.d
        [[ -f /opt/aob/infra/nginx/aob-zones.conf ]] && \
            sudo cp /opt/aob/infra/nginx/aob-zones.conf /etc/nginx/conf.d/aob-zones.conf
        # Mapas de redirect legacy
        [[ -f /opt/aob/infra/nginx/redirects.aob.map ]] && \
            sudo cp /opt/aob/infra/nginx/redirects.aob.map /etc/nginx/
        [[ -f /opt/aob/infra/nginx/redirects.bva.map ]] && \
            sudo cp /opt/aob/infra/nginx/redirects.bva.map /etc/nginx/
        # Symlinks sites-enabled
        for c in aobarcelos.pt bva-p.aobarcelos.pt api.aobarcelos.pt admin.aobarcelos.pt bva-p-socios.aobarcelos.pt; do
            [[ -f /etc/nginx/sites-available/$c.conf ]] && \
                sudo ln -sf /etc/nginx/sites-available/$c.conf /etc/nginx/sites-enabled/$c.conf
        done
        sudo rm -f /etc/nginx/sites-enabled/default
        sudo systemctl daemon-reload
        sudo nginx -t  # apenas valida; o 'services' arranca nginx apos parar Apache2
    '
}

install_next_global() {
    echo "==> Instalar next@$NEXT_VERSION globalmente no VPS"
    ssh "$SSH_TARGET" "sudo npm install -g next@$NEXT_VERSION"
    echo "==> Verificar"
    ssh "$SSH_TARGET" "next --version"
}

case "$TARGET" in
    api)          deploy_dotnet api AOB.Api /opt/aob/api aob-api ;;
    admin)        deploy_dotnet admin AOB.Admin /opt/aob/admin aob-admin ;;
    aobarcelos)   deploy_next aobarcelos aobarcelos /opt/aob/aobarcelos aob-aobarcelos ;;
    bva)          deploy_next bva bva-portugal /opt/aob/bva-portugal aob-bva-portugal ;;
    infra)        deploy_infra ;;
    install-next) install_next_global ;;
    all)
        deploy_infra
        install_next_global
        deploy_dotnet api AOB.Api /opt/aob/api aob-api
        deploy_dotnet admin AOB.Admin /opt/aob/admin aob-admin
        deploy_next aobarcelos aobarcelos /opt/aob/aobarcelos aob-aobarcelos
        deploy_next bva bva-portugal /opt/aob/bva-portugal aob-bva-portugal
        ;;
    *) echo "Uso: $0 [api|admin|aobarcelos|bva|infra|install-next|all]"; exit 1 ;;
esac

echo "==> Deploy '$TARGET' concluido."
