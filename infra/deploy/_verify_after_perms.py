import paramiko
import time
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

def run(cmd):
    _, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    return out, err

# Verificar permissoes PG
out, err = run("sudo -u postgres psql -d aob_prod -c \"SELECT COUNT(*) FROM sites;\" 2>&1")
print(f"Sites count (postgres): {out}")
if err: print(f"[err] {err}")

# Verificar grants
out, _ = run("sudo -u postgres psql -d aob_prod -c \"SELECT grantee, privilege_type FROM information_schema.role_table_grants WHERE table_name='sites' AND grantee='aobapp';\" 2>&1")
print(f"\nGrants em sites para aobapp:\n{out}")

# Reiniciar API
print("\nReiniciando aob-api...")
run("sudo systemctl restart aob-api")
time.sleep(4)

# Journal
out, _ = run("sudo journalctl -u aob-api -n 25 --no-pager")
print(f"\nJournal API:\n{out[-2500:]}")

# Testar endpoints
out, _ = run("curl -sf --max-time 10 http://127.0.0.1:5000/api/health 2>&1")
print(f"\nAPI /health: {out}")

out, _ = run("curl -sf --max-time 10 'http://127.0.0.1:5000/api/sites/aob' 2>&1 | head -c 400")
print(f"API /sites/aob: {out}")

out, _ = run("curl -sf --max-time 10 'http://127.0.0.1:5000/api/sites/aob/articles?pageSize=3' 2>&1 | head -c 400")
print(f"API /sites/aob/articles: {out}")

# Testar frontends via nginx
out, _ = run("curl -sf --max-time 10 -H 'Host: aobarcelos.pt' http://127.0.0.1/ 2>&1 | head -c 300")
print(f"\nFrontend aobarcelos: {out}")

out, _ = run("curl -sf --max-time 10 -H 'Host: bva-p.aobarcelos.pt' http://127.0.0.1/ 2>&1 | head -c 300")
print(f"Frontend bva: {out}")

ssh.close()
