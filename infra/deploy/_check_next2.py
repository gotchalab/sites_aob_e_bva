import paramiko
from pathlib import Path

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect("51.83.40.43", username="debian", key_filename=str(Path.home() / ".ssh" / "id_ed25519"))

cmds = [
    "ls /usr/lib/node_modules/next/dist/compiled/next-server/ | grep app-page",
    "cat /opt/aob/aobarcelos/.next/server/app/page.js | grep -m3 'runtime'",
    "cat /opt/aob/aobarcelos/.next/BUILD_ID",
    "cat /opt/aob/aobarcelos/.next/react-loadable-manifest.json 2>/dev/null | head -5 || echo N/A",
]

for c in cmds:
    print(f"\n=== {c[:80]} ===")
    _, stdout, stderr = ssh.exec_command(c)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    print(out[:500] if out else "[sem output]")
    if err.strip():
        print("[err]", err[:200])

ssh.close()
