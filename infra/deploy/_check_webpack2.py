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

# Procurar a funcao f.require no bva webpack-runtime
out, _ = run("grep -n 'f.require\\|require.*chunkId\\|chunkId.*require\\|installChunk\\|\.\/chunks' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1 | head -20")
print(f"bva f.require / chunkId:\n{out}\n")

out, _ = run("grep -n 'f.require\\|require.*chunkId\\|chunkId.*require\\|installChunk\\|\.\/chunks' /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1 | head -20")
print(f"aobarcelos f.require / chunkId:\n{out}\n")

# Ver a linha com require relativo e chunkId
out, _ = run("grep -n '__non_webpack_require__\\|require.*chunkId\\|chunkId\\+' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1 | head -10")
print(f"bva __non_webpack_require__:\n{out}\n")

out, _ = run("grep -n '__non_webpack_require__\\|require.*chunkId\\|chunkId\\+' /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1 | head -10")
print(f"aobarcelos __non_webpack_require__:\n{out}\n")

# Extrair a seccao do chunk loading do bva
out, _ = run("sed -n '180,215p' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1")
print(f"bva lines 180-215:\n{out}\n")

out, _ = run("sed -n '180,215p' /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1")
print(f"aobarcelos lines 180-215:\n{out}\n")

ssh.close()
