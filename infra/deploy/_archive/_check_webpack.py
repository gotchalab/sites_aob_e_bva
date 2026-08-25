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

# Ver onde o webpack-runtime procura chunks (a funcao de require de chunks)
out, _ = run("grep -o 'require(.*441' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1 | head -5")
print(f"bva webpack-runtime require 441:\n{out}\n")

out, _ = run("grep -o 'require(.*441' /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1 | head -5")
print(f"aobarcelos webpack-runtime require 441:\n{out}\n")

# Ver a funcao de carregamento de chunks no bva webpack-runtime
out, _ = run("grep -o '\"./chunks/\\.js\"\|\"\\./\\.js\"\|path\\.join\|__dirname' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1 | head -10")
print(f"bva chunk loading pattern:\n{out}\n")

# Comparar a linha que define o path dos chunks
out, _ = run("grep -n 'chunks/' /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1 | head -5")
print(f"bva 'chunks/' no webpack-runtime:\n{out}\n")

out, _ = run("grep -n 'chunks/' /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1 | head -5")
print(f"aobarcelos 'chunks/' no webpack-runtime:\n{out}\n")

# Tamanhos dos webpack-runtime
out, _ = run("wc -c /opt/aob/bva-portugal/.next/server/webpack-runtime.js /opt/aob/aobarcelos/.next/server/webpack-runtime.js 2>&1")
print(f"Tamanhos webpack-runtime:\n{out}\n")

# Ver os primeiros 200 chars do bva webpack-runtime
out, _ = run("head -c 500 /opt/aob/bva-portugal/.next/server/webpack-runtime.js 2>&1")
print(f"Inicio bva webpack-runtime:\n{out}\n")

ssh.close()
