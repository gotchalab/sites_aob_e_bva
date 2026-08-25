"""Diagnostico SMTP: le credenciais de /etc/aob/api.env e faz EHLO/STARTTLS/AUTH.
Nao envia email real. Nao imprime credenciais (sem debuglevel).
Corre com sudo para poder ler o api.env.
"""
import smtplib
import sys
import ssl

env = {}
with open("/etc/aob/api.env") as f:
    for line in f:
        line = line.strip()
        if line.startswith("Smtp__") and "=" in line:
            k, v = line.split("=", 1)
            env[k] = v

host = env.get("Smtp__Host", "")
port = int(env.get("Smtp__Port", "587"))
user = env.get("Smtp__User", "")
pw   = env.get("Smtp__Password", "")
use_ssl = env.get("Smtp__UseSsl", "false").lower() == "true"

user_mask = user[:3] + "***" + user[-4:] if len(user) > 8 else "***"
print(f"host={host}  port={port}  user_masked={user_mask}  password_len={len(pw)}  use_ssl={use_ssl}")

try:
    s = smtplib.SMTP(host, port, timeout=20)
    code, msg = s.ehlo()
    print(f"EHLO code={code}")
    if use_ssl:
        s.starttls(context=ssl.create_default_context())
        s.ehlo()
        print("STARTTLS ok")
    print(f"AUTH mechanisms advertised: {s.esmtp_features.get('auth', '(none)')}")
    s.login(user, pw)
    print("=== AUTH OK ===")
    s.quit()
except smtplib.SMTPAuthenticationError as e:
    print(f"=== AUTH FAILED === code={e.smtp_code}")
    err = e.smtp_error.decode(errors='replace') if isinstance(e.smtp_error, bytes) else str(e.smtp_error)
    print(f"server_msg={err}")
    sys.exit(2)
except Exception as e:
    print(f"=== ERROR === {type(e).__name__}: {e}")
    sys.exit(3)
