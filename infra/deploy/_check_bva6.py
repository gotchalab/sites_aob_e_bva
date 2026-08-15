import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko, time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd, timeout=30):
    _, stdout, stderr = ssh.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Reiniciar servico bva
print("Reiniciando bva-portugal...")
run("sudo systemctl restart aob-bva-portugal")
time.sleep(5)

out, _ = run("sudo systemctl is-active aob-bva-portugal")
print(f"Status bva: {out}")

# Testar curl directo
out, _ = run("curl -s --max-time 10 http://127.0.0.1:3001/contacto 2>&1 | head -c 200")
print(f"\nCurl /contacto: {out}")

out, _ = run("curl -s --max-time 10 http://127.0.0.1:3001/_next/static/ 2>&1 | head -c 100")
print(f"Curl /_next/static/: {out}")

# Ver o que esta no index.html pre-renderizado
out, _ = run("head -5 /opt/aob/bva-portugal/.next/server/app/index.html 2>&1")
print(f"\nindex.html preview:\n{out}")

# Ver o slug baked no bundle JS
out, _ = run("grep -r 'NEXT_PUBLIC_SITE_SLUG\\|site_slug\\|siteSlug' /opt/aob/bva-portugal/.next/server/chunks/ 2>/dev/null | head -3")
print(f"\nSlug no bundle server:\n{out}")

# Verificar se ha erro de middleware - ver o middleware.js
out, _ = run("ls /opt/aob/bva-portugal/.next/server/ | grep -i middleware")
print(f"\nMiddleware files: {out}")

out, _ = run("curl -v --max-time 10 http://127.0.0.1:3001/ 2>&1 | grep -E 'HTTP|Location|< ' | head -20")
print(f"\nCurl / com headers:\n{out}")

ssh.close()
