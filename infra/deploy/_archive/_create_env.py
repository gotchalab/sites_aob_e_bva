#!/usr/bin/env python3
"""Cria /etc/aob/*.env no VPS com os valores de producao conhecidos."""
import io
import paramiko
from pathlib import Path

VPS_HOST = "51.83.40.43"
VPS_USER = "debian"
VPS_KEY  = str(Path.home() / ".ssh" / "id_ed25519")

PG_PASS   = "EM3A31tTxtXVpPfJOc2DmcWfoyE+FKm2"
JWT_KEY   = "jF2tSokdPXjc8cEwOOmIcb8m0mI0eEhgKcxOIEAwTsresVQKH+35yOJPNXB7jTe1"
BREVO_KEY = "xsmtpsib-d7c49a67a9400c9d6b1c4e561ab3f5d66b902bd6b5982f58ce63eb136998cd38-uawiv0rB1NPq9U5w"
# Cloudflare Turnstile test keys (always-pass) — substituir apos configurar conta Cloudflare
TS_SITEKEY = "1x00000000000000000000AA"
TS_SECRET  = "1x0000000000000000000000000000000AA"

CONN = f"Host=localhost;Port=5432;Database=aob_prod;Username=aobapp;Password={PG_PASS}"

ENV_FILES = {
    "/etc/aob/api.env": f"""ConnectionStrings__Default={CONN}
Uploads__RootPath=/var/www/uploads
Cors__Origins__0=https://aobarcelos.pt
Cors__Origins__1=https://bva-p.aobarcelos.pt
Turnstile__SecretKey={TS_SECRET}
Jwt__SigningKey={JWT_KEY}
Jwt__Issuer=aob
Jwt__Audience=aob-clients
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=30
Smtp__Host=smtp-relay.brevo.com
Smtp__Port=587
Smtp__UseSsl=true
Smtp__User=noreply.bva.aob@gmail.com
Smtp__Password={BREVO_KEY}
""",
    "/etc/aob/admin.env": f"""ConnectionStrings__Default={CONN}
Uploads__RootPath=/var/www/uploads
""",
    "/etc/aob/aobarcelos.env": f"""NEXT_PUBLIC_API_URL=https://aobarcelos.pt
NEXT_PUBLIC_SITE_SLUG=aob
NEXT_PUBLIC_TURNSTILE_SITEKEY={TS_SITEKEY}
API_INTERNAL_URL=http://127.0.0.1:5000
""",
    "/etc/aob/bva-portugal.env": f"""NEXT_PUBLIC_API_URL=https://bva-p.aobarcelos.pt
NEXT_PUBLIC_SITE_SLUG=bva
NEXT_PUBLIC_TURNSTILE_SITEKEY={TS_SITEKEY}
API_INTERNAL_URL=http://127.0.0.1:5000
""",
}

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect(VPS_HOST, username=VPS_USER, key_filename=VPS_KEY)

for path, content in ENV_FILES.items():
    fname = path.split("/")[-1]
    tmp = f"/tmp/aob_env_{fname}"
    sftp = ssh.open_sftp()
    sftp.putfo(io.BytesIO(content.encode()), tmp)
    sftp.close()
    _, stdout, stderr = ssh.exec_command(
        f"sudo mv {tmp} {path} && sudo chmod 640 {path} && sudo chown root:root {path}"
    )
    code = stdout.channel.recv_exit_status()
    err = stderr.read().decode().strip()
    if err:
        print(f"  [stderr] {err}")
    print(f"  {'OK' if code == 0 else 'ERRO'} {path}")

ssh.close()
print("Env files criados.")
