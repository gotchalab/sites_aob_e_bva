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

# Verificar porta do admin
out, _ = run("sudo ss -tlnp | grep -E ':5[0-9]{3}'")
print(f"Portas 5xxx:\n{out}\n")

out, _ = run("sudo cat /etc/aob/admin.env 2>&1")
print(f"Admin env:\n{out}\n")

out, _ = run("sudo systemctl cat aob-admin | head -30")
print(f"Admin service:\n{out}\n")

# Verificar journal admin
out, _ = run("sudo journalctl -u aob-admin -n 10 --no-pager --output=cat")
print(f"Journal admin:\n{out}\n")

# Corrigir EROFS: adicionar ReadWritePaths aos servicos Next.js
for svc_name, app_dir in [("aob-aobarcelos", "/opt/aob/aobarcelos"), ("aob-bva-portugal", "/opt/aob/bva-portugal")]:
    svc_file = f"/etc/systemd/system/{svc_name}.service"

    # Verificar se ja tem ReadWritePaths
    out, _ = run(f"grep 'ReadWritePaths' {svc_file} 2>&1")
    if "ReadWritePaths" in out:
        print(f"{svc_name}: ja tem ReadWritePaths")
        continue

    # Adicionar antes de NoNewPrivileges
    run(f"sudo sed -i 's|^NoNewPrivileges=true|ReadWritePaths={app_dir}/.next\\nNoNewPrivileges=true|' {svc_file}")
    print(f"{svc_name}: ReadWritePaths adicionado")

# Recarregar systemd e reiniciar servicos
run("sudo systemctl daemon-reload")
run("sudo systemctl restart aob-aobarcelos aob-bva-portugal")
time.sleep(5)

out, _ = run("sudo systemctl is-active aob-aobarcelos aob-bva-portugal")
print(f"\nEstado apos restart:\n{out}")

# Testar que continuam a funcionar
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:3000/ 2>&1")
print(f"aobarcelos:3000 / -> HTTP {out}")
out, _ = run("curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:3001/ 2>&1")
print(f"bva:3001 / -> HTTP {out}")

# Verificar se EROFS desapareceu
time.sleep(3)
out, _ = run("sudo journalctl -u aob-bva-portugal --since 'now' -n 5 --no-pager --output=cat 2>&1 || sudo journalctl -u aob-bva-portugal -n 5 --no-pager --output=cat")
print(f"\nJournal bva apos restart:\n{out}")

ssh.close()
