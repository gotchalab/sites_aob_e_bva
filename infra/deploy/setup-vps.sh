#!/usr/bin/env bash
# Bootstrap Debian 12 VPS para hospedar AOB (aobarcelos.pt + bva-p.aobarcelos.pt + api + admin).
# Correr como root em VPS fresco. Idempotente — pode correr varias vezes.
set -euo pipefail

log() { echo -e "\n\033[1;34m==> $*\033[0m"; }
warn() { echo -e "\033[1;33m[warn] $*\033[0m"; }

if [[ $EUID -ne 0 ]]; then
    echo "Correr como root." >&2
    exit 1
fi

: "${AOB_PG_PASSWORD:?Defina AOB_PG_PASSWORD antes de correr (password do user aobapp na BD)}"
: "${AOB_ADMIN_EMAIL:?Defina AOB_ADMIN_EMAIL (para certbot)}"
: "${AOB_SSH_KEY:?Defina AOB_SSH_KEY com a chave publica SSH a autorizar}"

DEBIAN_FRONTEND=noninteractive
export DEBIAN_FRONTEND

log "1. Actualizar sistema + tools basicas"
apt-get update
apt-get -y upgrade
apt-get -y install curl wget gnupg2 ca-certificates apt-transport-https lsb-release \
    ufw fail2ban unattended-upgrades apt-listchanges rsync git jq htop unzip

log "2. Instalar PostgreSQL 16"
if ! command -v psql >/dev/null; then
    sh -c 'echo "deb http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
    wget --quiet -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg
    sed -i 's|deb http|deb [signed-by=/usr/share/keyrings/postgresql.gpg] http|' /etc/apt/sources.list.d/pgdg.list
    apt-get update
    apt-get -y install postgresql-16
fi
systemctl enable --now postgresql

# Escuta apenas em localhost
PG_CONF=$(sudo -u postgres psql -tAc "SHOW config_file;")
if ! grep -qE "^listen_addresses = 'localhost'" "$PG_CONF"; then
    sed -i "s|#listen_addresses = 'localhost'|listen_addresses = 'localhost'|" "$PG_CONF"
    sed -i "s|^listen_addresses = '.*'|listen_addresses = 'localhost'|" "$PG_CONF"
    systemctl restart postgresql
fi

# BD + user (idempotente)
sudo -u postgres psql <<SQL
DO \$\$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aobapp') THEN
    CREATE ROLE aobapp WITH LOGIN PASSWORD '$AOB_PG_PASSWORD';
  END IF;
END \$\$;
SQL
sudo -u postgres psql <<SQL || true
CREATE DATABASE aob_prod OWNER aobapp;
SQL

log "3. Instalar .NET 10 runtime"
if ! command -v dotnet >/dev/null; then
    wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O /tmp/msprod.deb
    dpkg -i /tmp/msprod.deb
    apt-get update
    apt-get -y install aspnetcore-runtime-10.0
fi

log "4. Instalar Node.js 22"
if ! command -v node >/dev/null || [[ $(node -v) != v22* ]]; then
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
    apt-get -y install nodejs
fi

log "5. Instalar Nginx + Certbot"
apt-get -y install nginx certbot python3-certbot-nginx
systemctl enable --now nginx

log "6. Instalar ClamAV (deteção de malware nos uploads)"
apt-get -y install clamav clamav-daemon
systemctl enable --now clamav-freshclam
# freshclam pode precisar de tempo para descarregar as bases

# NOTA: nao instalamos MTA local — o EmailSender aponta directamente para Brevo
# (smtp-relay.brevo.com:587) via /etc/aob/api.env. Se um dia for preciso enviar
# mail de sistema (cron/fail2ban/etc.) instalar exim4-daemon-light e configurar
# smarthost para o Brevo.

log "7. Utilizadores dos servicos"
for u in aob-api aob-admin aob-web; do
    if ! id -u "$u" >/dev/null 2>&1; then
        useradd -r -M -s /usr/sbin/nologin "$u"
    fi
done
# aob-web tem de ler /var/www/uploads via symlink public/uploads para o
# Next.js image optimizer funcionar (Next 15 le URLs /uploads/... do FS).
# aob-admin tambem tem de escrever (uploads via CKEditor picker no backoffice).
usermod -aG www-data aob-web
usermod -aG www-data aob-admin

log "8. Estrutura de diretorios"
install -d -o aob-api   -g aob-api   -m 0755 /opt/aob/api
install -d -o aob-admin -g aob-admin -m 0755 /opt/aob/admin
install -d -o aob-web   -g aob-web   -m 0755 /opt/aob/aobarcelos
install -d -o aob-web   -g aob-web   -m 0755 /opt/aob/bva-portugal
install -d -o aob-api   -g www-data  -m 2770 /var/www/uploads
# 2770 = setgid + group rwx. setgid faz novos subdirs herdarem grupo www-data
# (para aob-web ler via symlink public/uploads no Next); group write permite
# ao aob-admin (backoffice CKEditor) e ao aob-api (API) criar ficheiros no
# mesmo tree. Reaplica em subdirs existentes.
find /var/www/uploads -type d -exec chgrp www-data {} + -exec chmod 2770 {} +
find /var/www/uploads -type f -exec chmod 0660 {} +
install -d -o root      -g root      -m 0755 /etc/aob
install -d -o root      -g root      -m 0755 /var/log/aob

log "9. Firewall (ufw)"
ufw --force reset >/dev/null
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

log "10. SSH hardening"
mkdir -p /home/debian/.ssh
echo "$AOB_SSH_KEY" > /home/debian/.ssh/authorized_keys
chown -R debian:debian /home/debian/.ssh
chmod 700 /home/debian/.ssh
chmod 600 /home/debian/.ssh/authorized_keys

if ! grep -q "^PasswordAuthentication no" /etc/ssh/sshd_config; then
    sed -i 's|^#*PasswordAuthentication .*|PasswordAuthentication no|' /etc/ssh/sshd_config
fi
if ! grep -q "^PermitRootLogin no" /etc/ssh/sshd_config; then
    sed -i 's|^#*PermitRootLogin .*|PermitRootLogin no|' /etc/ssh/sshd_config
fi
systemctl restart ssh || systemctl restart sshd || warn "Nao consegui reiniciar SSH"

log "11. fail2ban"
cat > /etc/fail2ban/jail.local <<'EOF'
[DEFAULT]
bantime = 1h
findtime = 10m
maxretry = 5

[sshd]
enabled = true

[nginx-http-auth]
enabled = true
port = http,https
EOF
systemctl restart fail2ban

log "12. unattended-upgrades"
dpkg-reconfigure -f noninteractive unattended-upgrades
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF

log "13. Instalar systemd units (copiadas de /opt/aob/infra/systemd)"
if [[ -d /opt/aob/infra/systemd ]]; then
    cp /opt/aob/infra/systemd/*.service /etc/systemd/system/
    systemctl daemon-reload
fi

log "14. Configuracao Nginx"
if [[ -d /opt/aob/infra/nginx ]]; then
    cp /opt/aob/infra/nginx/*.conf /etc/nginx/sites-available/
    for conf in /opt/aob/infra/nginx/*.conf; do
        name=$(basename "$conf")
        ln -sf "/etc/nginx/sites-available/$name" "/etc/nginx/sites-enabled/$name"
    done
    rm -f /etc/nginx/sites-enabled/default
    nginx -t && systemctl reload nginx
fi

log "Setup concluido."
echo
echo "==============================================="
echo "Proximos passos manuais:"
echo "==============================================="
echo ""
echo "  0. Verificar/parar Apache2 (corre na porta 80, conflito com Nginx):"
echo "       systemctl stop apache2 && systemctl disable apache2"
echo ""
echo "  1. Criar /etc/aob/api.env e /etc/aob/admin.env (copiar dos env.sample e preencher):"
echo "       nano /etc/aob/api.env"
echo "     Valores obrigatorios: ConnectionStrings__Default, Jwt__SigningKey, Smtp__Password, Smtp__From"
echo ""
echo "  2. Corrigir password BD do bvaproject (se ainda com password antiga):"
echo "     Verificar: cat /opt/bvaproject/appsettings.Production.json | grep -i password"
echo "     Corrigir:  nano /opt/bvaproject/appsettings.Production.json"
echo ""
echo "  3. Trocar JWT Key fraca do bvaproject:"
echo "     nano /opt/bvaproject/appsettings.json  # campo Tokens.Key"
echo ""
echo "  4. Deploy inicial dos binarios AOB:"
echo "       AOB_SSH_HOST=51.83.40.43 bash deploy.sh all"
echo ""
echo "  5. Certbot para todos os dominios novos:"
echo "       certbot --nginx -d aobarcelos.pt -d www.aobarcelos.pt -d bva-p.aobarcelos.pt -d api.aobarcelos.pt -d admin.aobarcelos.pt --email $AOB_ADMIN_EMAIL --agree-tos --redirect"
echo "     Para bva-p-socios (cert ja existe — renovar com nginx):"
echo "       certbot --nginx -d bva-p-socios.aobarcelos.pt --email $AOB_ADMIN_EMAIL --agree-tos --redirect"
echo ""
echo "  6. Arrancar todos os servicos AOB:"
echo "       systemctl start aob-api aob-admin aob-aobarcelos aob-bva-portugal"
echo "       systemctl restart bvaproject  # ou o nome do servico existente"
echo ""
echo "  7. Correr migracoes e seed:"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator seed"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator migrate categories"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator migrate articles"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator migrate downloads"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator migrate menus"
echo "       sudo -u aob-api /opt/aob/api/AOB.Migrator migrate images"
echo ""
echo "  8. Verificar que tudo esta a funcionar:"
echo "       curl -s https://aobarcelos.pt | head -5"
echo "       curl -s https://bva-p-socios.aobarcelos.pt | head -5"
echo "       systemctl status aob-api aob-admin aob-aobarcelos aob-bva-portugal"
