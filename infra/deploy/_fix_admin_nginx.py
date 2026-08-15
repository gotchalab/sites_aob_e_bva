import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=20):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# 1. Verificar se o map já existe no nginx.conf
print("=== nginx.conf: map ws_connection ===")
out, _ = run("grep -n 'connection_upgrade\\|http_upgrade' /etc/nginx/nginx.conf")
print(out or "(sem map)")

# 2. Adicionar map ao nginx.conf (no bloco http, antes do include)
out, _ = run("grep -c 'connection_upgrade' /etc/nginx/nginx.conf")
if out.strip() == "0":
    print("\nA adicionar map ao nginx.conf...")
    # Inserir depois de "http {"
    run("""sudo sed -i '/^http {/a\\\\tmap $http_upgrade $connection_upgrade {\\n\\t\\tdefault upgrade;\\n\\t\\t\\x27\\x27      close;\\n\\t}' /etc/nginx/nginx.conf""")
    out, _ = run("grep -n 'connection_upgrade' /etc/nginx/nginx.conf")
    print(f"  Após insert: {out}")
else:
    print("  map já existe")

# 3. Nova config do admin (substitui a gerada pelo certbot)
admin_conf = r"""# admin.aobarcelos.pt — backoffice Blazor Server

server {
    server_name admin.aobarcelos.pt;

    include /etc/nginx/snippets/aob-common.conf;

    location / {
        limit_req zone=aob_public burst=30 nodelay;
        proxy_pass http://127.0.0.1:5001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 300s;
    }

    location /uploads/ {
        alias /var/www/uploads/;
        add_header X-Content-Type-Options "nosniff" always;
        try_files $uri =404;
    }

    client_max_body_size 60M;

    listen [::]:443 ssl; # managed by Certbot
    listen 443 ssl; # managed by Certbot
    ssl_certificate /etc/letsencrypt/live/admin.aobarcelos.pt/fullchain.pem; # managed by Certbot
    ssl_certificate_key /etc/letsencrypt/live/admin.aobarcelos.pt/privkey.pem; # managed by Certbot
    include /etc/letsencrypt/options-ssl-nginx.conf; # managed by Certbot
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem; # managed by Certbot
}

server {
    if ($host = admin.aobarcelos.pt) {
        return 301 https://$host$request_uri;
    } # managed by Certbot

    listen 80;
    listen [::]:80;
    server_name admin.aobarcelos.pt;
    return 404; # managed by Certbot
}
"""

sftp = ssh.open_sftp()
sftp.putfo(io.BytesIO(admin_conf.encode()), "/tmp/admin.conf")
sftp.close()
run("sudo cp /tmp/admin.conf /etc/nginx/sites-enabled/admin.aobarcelos.pt.conf")
run("sudo rm /tmp/admin.conf")

# 4. Testar e recarregar
print("\n=== nginx -t ===")
out, err = run("sudo nginx -t 2>&1")
print(out or err)

if "ok" in (out + err).lower():
    run("sudo systemctl reload nginx")
    print("nginx recarregado")

# 5. Teste final
import time
time.sleep(2)
print("\n=== Teste HTTPS admin ===")
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 10 https://admin.aobarcelos.pt/ 2>&1")
print(f"  https://admin.aobarcelos.pt/ -> {out}")

out, _ = run("curl -s -L --max-time 10 -o /dev/null -w '%{http_code}' https://admin.aobarcelos.pt/login 2>&1")
print(f"  https://admin.aobarcelos.pt/login -> {out}")

ssh.close()
