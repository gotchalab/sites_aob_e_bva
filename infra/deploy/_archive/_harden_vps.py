import sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=60):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

def ok(label, out, err=""):
    status = out or err or "(sem saída)"
    print(f"  [{label}] {status[:120]}")

# 1. Verificar estado SSH atual
print("=== SSH CONFIG ATUAL ===")
out, _ = run("grep -E 'PasswordAuthentication|PermitRootLogin|PubkeyAuthentication' /etc/ssh/sshd_config")
ok("sshd_config", out)

# Verificar chaves autorizadas
out, _ = run("cat ~/.ssh/authorized_keys 2>/dev/null | head -3")
ok("authorized_keys", out[:80])

# 2. Instalar fail2ban e ufw
print("\n=== INSTALAR fail2ban + ufw ===")
out, err = run("sudo apt-get install -y fail2ban ufw 2>&1 | tail -5", timeout=120)
ok("apt install", out or err)

# 3. Configurar ufw
print("\n=== CONFIGURAR UFW ===")
# Garantir SSH antes de enable
run("sudo ufw allow 22/tcp")
run("sudo ufw allow 80/tcp")
run("sudo ufw allow 443/tcp")
out, _ = run("sudo ufw --force enable")
ok("ufw enable", out)
out, _ = run("sudo ufw status numbered")
ok("ufw status", out)

# 4. Configurar fail2ban
print("\n=== CONFIGURAR fail2ban ===")
jail_local = """[DEFAULT]
bantime  = 3600
findtime = 600
maxretry = 5
backend  = auto

[sshd]
enabled = true
port    = ssh
logpath = %(sshd_log)s
backend = %(sshd_backend)s

[nginx-http-auth]
enabled = true

[nginx-limit-req]
enabled  = true
filter   = nginx-limit-req
action   = iptables-multiport[name=ReqLimit, port="http,https", protocol=tcp]
logpath  = /var/log/nginx/error.log
findtime = 600
bantime  = 7200
maxretry = 10
"""

sftp = ssh.open_sftp()
import io as _io
sftp.putfo(_io.BytesIO(jail_local.encode()), "/tmp/jail.local")
sftp.close()
run("sudo cp /tmp/jail.local /etc/fail2ban/jail.local")
run("sudo systemctl enable fail2ban")
run("sudo systemctl restart fail2ban")
time.sleep(2)
out, _ = run("sudo systemctl is-active fail2ban")
ok("fail2ban status", out)
out, _ = run("sudo fail2ban-client status 2>&1 | head -10")
ok("fail2ban jails", out)

# 5. Desativar autenticação por password (já temos chave ED25519)
print("\n=== SSH HARDENING ===")
# Verificar que temos authorized_keys com conteúdo
out, _ = run("wc -l ~/.ssh/authorized_keys 2>/dev/null")
key_count = out.strip()
print(f"  authorized_keys: {key_count} linha(s)")

if key_count and key_count != "0":
    # Desativar password auth
    run("sudo sed -i 's/^#*PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config")
    run("sudo sed -i 's/^#*PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config")
    # Adicionar se não existir
    out, _ = run("grep -c '^PasswordAuthentication no' /etc/ssh/sshd_config")
    if out.strip() == "0":
        run("echo 'PasswordAuthentication no' | sudo tee -a /etc/ssh/sshd_config")
    run("sudo systemctl reload sshd")
    out, _ = run("grep -E '^PasswordAuthentication|^PermitRootLogin' /etc/ssh/sshd_config")
    ok("sshd após hardening", out)
else:
    print("  AVISO: authorized_keys vazio — a NÃO desativar PasswordAuthentication (risco de lockout)")

# 6. Verificar unattended-upgrades
print("\n=== UNATTENDED-UPGRADES ===")
out, _ = run("dpkg -l unattended-upgrades 2>/dev/null | grep '^ii'")
if not out:
    run("sudo apt-get install -y unattended-upgrades 2>&1", timeout=60)
    run("sudo dpkg-reconfigure -plow unattended-upgrades 2>&1 || true")
out, _ = run("systemctl is-active unattended-upgrades 2>/dev/null || echo 'não activo'")
ok("unattended-upgrades", out)

# 7. Verificar certbot timer
print("\n=== CERTBOT RENEWAL TIMER ===")
out, _ = run("sudo systemctl is-active certbot.timer 2>/dev/null || sudo systemctl is-active snap.certbot.renew.timer 2>/dev/null || echo 'timer não encontrado'")
ok("certbot timer", out)
out, _ = run("sudo certbot renew --dry-run 2>&1 | tail -5")
ok("certbot dry-run", out)

# 8. Resumo de portos em escuta (só localhost ou públicos)
print("\n=== PORTOS EM ESCUTA ===")
out, _ = run("sudo ss -tlnp | grep -v '127.0.0.1\\|::1'")
ok("portos públicos", out)

# 9. Estado geral dos serviços AOB
print("\n=== ESTADO SERVIÇOS AOB ===")
for svc in ["aob-api", "aob-admin", "aob-aobarcelos", "aob-bva-portugal"]:
    out, _ = run(f"sudo systemctl is-active {svc} 2>/dev/null")
    print(f"  {svc}: {out}")

ssh.close()
print("\n=== HARDENING CONCLUÍDO ===")
