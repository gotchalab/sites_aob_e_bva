import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Testar API para site bva
out, _ = run("curl -sf --max-time 10 'http://127.0.0.1:5000/api/sites/bva' 2>&1 | head -c 400")
print(f"API /sites/bva: {out}\n")

# Testar API artigos bva
out, _ = run("curl -sf --max-time 10 'http://127.0.0.1:5000/api/sites/bva/articles?pageSize=1' 2>&1 | head -c 300")
print(f"API /sites/bva/articles: {out}\n")

# Ver o que o bva retorna com mais detalhe (stderr do node)
out, _ = run("sudo journalctl -u aob-bva-portugal -n 5 --no-pager")
print(f"Journal bva (depois do curl):\n{out}\n")

# Ver env vars reais no processo bva (como systemd as passa)
out, _ = run("sudo cat /proc/$(sudo systemctl show -p MainPID aob-bva-portugal | cut -d= -f2)/environ 2>/dev/null | tr '\\0' '\\n' | grep -E 'API|SLUG|NODE' 2>&1")
print(f"Env vars no processo bva:\n{out}\n")

# Ver next.config.mjs do bva
out, _ = run("cat /opt/aob/bva-portugal/next.config.mjs 2>&1")
print(f"next.config.mjs bva:\n{out}\n")

# Ver o build id do bva vs aobarcelos
out, _ = run("cat /opt/aob/bva-portugal/.next/BUILD_ID 2>&1")
print(f"bva BUILD_ID: {out}")
out, _ = run("cat /opt/aob/aobarcelos/.next/BUILD_ID 2>&1")
print(f"aobarcelos BUILD_ID: {out}")

ssh.close()
