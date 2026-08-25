import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=30):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

print("=== ESTADO DO VPS ===\n")

print("-- UFW --")
out, _ = run("sudo ufw status numbered")
print(out)

print("\n-- fail2ban --")
out, _ = run("sudo fail2ban-client status 2>&1")
print(out)

print("\n-- SSH --")
out, _ = run("grep -E '^PasswordAuthentication|^PermitRootLogin' /etc/ssh/sshd_config")
print(out)

print("\n-- Portos públicos (ex-localhost) --")
out, _ = run("sudo ss -tlnp 2>/dev/null | grep -v '127.0.0.1\\|::1\\|\\[::1\\]'")
print(out)

print("\n-- Serviços AOB --")
for svc in ["aob-api", "aob-admin", "aob-aobarcelos", "aob-bva-portugal", "bvaproject", "nginx", "postgresql"]:
    out, _ = run(f"systemctl is-active {svc} 2>/dev/null")
    print(f"  {svc}: {out}")

print("\n-- Certs Let's Encrypt --")
out, _ = run("sudo certbot certificates 2>/dev/null | grep -E 'Domains:|Expiry'")
print(out)

print("\n-- Nginx vhosts activos --")
out, _ = run("ls /etc/nginx/sites-enabled/")
print(out)

print("\n-- Testes HTTP finais --")
for domain, path in [
    ("aobarcelos.pt", "/"),
    ("bva-p.aobarcelos.pt", "/"),
    ("bva-p.aobarcelos.pt", "/artigos"),
]:
    out, _ = run(f"curl -s -o /dev/null -w '%{{http_code}}' --max-time 8 https://{domain}{path} 2>&1")
    print(f"  https://{domain}{path} -> {out}")

ssh.close()
