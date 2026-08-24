"""Converte a string de conexao da Neon (formato URI) para o formato do Npgsql.

A Neon entrega `postgresql://usuario:senha@host/banco?sslmode=require`. O Npgsql nao aceita
URI: ele espera `Host=...;Database=...;Username=...;Password=...`. Colar a URI direto produz
um erro de formato que nao diz o que esta errado.

Le e reescreve `backend/.env.neon` NO LUGAR, e nunca imprime a senha — so' confirma quais
campos foram reconhecidos.

    python backend/converter-conexao.py
"""

import io
import os
import sys
from urllib.parse import urlparse, parse_qs, unquote

CAMINHO = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".env.neon")


def converter(bruto: str) -> str:
    bruto = bruto.strip()

    if not bruto.startswith(("postgres://", "postgresql://")):
        # Ja esta em chave-valor: so' garante o SSL, que a Neon exige.
        if "ssl mode" not in bruto.lower() and "sslmode" not in bruto.lower():
            bruto = bruto.rstrip(";") + ";Ssl Mode=Require"
        return bruto

    u = urlparse(bruto)
    consulta = parse_qs(u.query)

    partes = [
        f"Host={u.hostname}",
        f"Port={u.port or 5432}",
        f"Database={unquote(u.path.lstrip('/'))}",
        f"Username={unquote(u.username or '')}",
        f"Password={unquote(u.password or '')}",
    ]

    # A Neon so' aceita conexao cifrada; `require` do libpq equivale a `Require` do Npgsql.
    modo = (consulta.get("sslmode") or ["require"])[0].lower()
    partes.append("Ssl Mode=" + ("Require" if modo in {"require", "verify-ca", "verify-full"} else "Prefer"))

    return ";".join(partes)


def main() -> int:
    if not os.path.exists(CAMINHO):
        print(f"Nao encontrei {CAMINHO}", file=sys.stderr)
        return 1

    original = io.open(CAMINHO, encoding="utf-8").read()
    convertida = converter(original)

    io.open(CAMINHO, "w", encoding="utf-8", newline="\n").write(convertida + "\n")

    # So' os NOMES dos campos. O valor nunca sai daqui.
    campos = [p.split("=", 1)[0] for p in convertida.split(";") if "=" in p]
    print("convertida. campos:", ", ".join(campos))
    print("pooler no host:", "-pooler" in convertida)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
