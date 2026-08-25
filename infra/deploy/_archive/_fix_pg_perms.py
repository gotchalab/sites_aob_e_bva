import io
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

sql = """
-- Dar permissoes ao aobapp sobre todos os objectos do schema public
GRANT USAGE ON SCHEMA public TO aobapp;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO aobapp;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO aobapp;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO aobapp;

-- Garantir defaults para objectos criados no futuro (migracoes)
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO aobapp;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO aobapp;
"""

tmp = "/tmp/aob_fix_perms.sql"
sftp = ssh.open_sftp()
sftp.putfo(io.BytesIO(sql.encode()), tmp)
sftp.close()

out, err = run(f"sudo -u postgres psql -d aob_prod -f {tmp}")
if out: print(out)
if err: print("[err]", err)
run(f"sudo rm -f {tmp}")

# Verificar
out, _ = run("sudo -u postgres psql -d aob_prod -c 'SELECT COUNT(*) FROM sites;' 2>&1")
print(f"\nSites count (como postgres): {out}")

out, _ = run("sudo -u aob-api psql -U aobapp -d aob_prod -h localhost -c 'SELECT COUNT(*) FROM sites;' 2>&1")
print(f"Sites count (como aob-api): {out}")

# Reiniciar API e testar
run("sudo systemctl restart aob-api")
import time; time.sleep(3)
out, _ = run("curl -sf --max-time 10 http://127.0.0.1:5000/api/health 2>&1")
print(f"\nAPI /health: {out}")
out, _ = run("curl -sf --max-time 10 'http://127.0.0.1:5000/api/sites/aob' 2>&1 | head -c 200")
print(f"API /sites/aob: {out}")

ssh.close()
