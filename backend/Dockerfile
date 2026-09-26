# syntax=docker/dockerfile:1
#
# Imagem da API e do worker, num Dockerfile só.
#
# Um arquivo com dois alvos, e não dois arquivos: as camadas de restore e build são idênticas
# — mesma solution, mesmos pacotes — e separá-las faria o cache do Docker reconstruir tudo
# duas vezes por deploy.
#
# Constrói com (a partir da raiz do repositório):
#   docker build -f backend/Dockerfile --target api    -t explanando-api    backend
#   docker build -f backend/Dockerfile --target worker -t explanando-worker backend
#
# E para a VPS da Oracle, que é ARM (ADR-0019), de qualquer máquina:
#   docker buildx build --platform linux/arm64 -f backend/Dockerfile --target api backend

# ── Restauração ──────────────────────────────────────────────────────────────────────────
#
# **O SDK roda na arquitetura de quem constrói, e não na de destino.** `$BUILDPLATFORM` é a
# máquina do build; `$TARGETARCH` é a da imagem final. O .NET compila IL, que não tem
# arquitetura — só o host de execução tem —, então publicar para arm64 a partir de um x64 é
# uma troca de parâmetro (`-a`), não uma emulação. Rodar o SDK dentro do QEMU funcionaria,
# mas leva dez vezes mais e às vezes trava no meio do restore sem dizer por quê.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS restauracao
ARG TARGETARCH
WORKDIR /origem

# Copia só os arquivos de projeto antes do código. É o que faz o `restore` — a parte lenta —
# reaproveitar cache enquanto nenhuma dependência mudar. Copiar tudo de uma vez invalidaria
# essa camada a cada linha de C# alterada.
#
# O .editorconfig vem junto, e não é detalhe de estilo: é ele que marca
# `src/**/Migrations/*.cs` como `generated_code = true`. Sem ele aqui dentro, os analisadores
# reprovam o código que o próprio EF Core gerou (CA1861/CA1825) e, com
# TreatWarningsAsErrors, o build do contêiner falha enquanto o local passa.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/ .config/
COPY src/Explanando.Domain/*.csproj          src/Explanando.Domain/
COPY src/Explanando.Application/*.csproj     src/Explanando.Application/
COPY src/Explanando.Infrastructure/*.csproj  src/Explanando.Infrastructure/
COPY src/Explanando.Api/*.csproj             src/Explanando.Api/
COPY src/Explanando.Worker/*.csproj          src/Explanando.Worker/

# Restaura só os dois executáveis, e não a solution: os projetos de teste não entram na
# imagem, e restaurá-los para `linux-arm64` seria baixar pacotes que ninguém vai usar.
# O `-a` precisa estar aqui E no publish — o restore grava os ativos por runtime, e um
# publish para outra arquitetura sem restore correspondente falha com NETSDK1047.
RUN dotnet restore src/Explanando.Api/Explanando.Api.csproj -a $TARGETARCH \
 && dotnet restore src/Explanando.Worker/Explanando.Worker.csproj -a $TARGETARCH

# ── Publicação ───────────────────────────────────────────────────────────────────────────
FROM restauracao AS publicacao
ARG TARGETARCH
COPY src/ src/

# `--no-restore` porque a camada acima já restaurou. Sem ele, o Docker faria a rede de novo
# a cada build, jogando fora exatamente o cache que a separação existe para criar.
#
# `--no-self-contained` porque a imagem de base já traz o runtime; e `UseAppHost=false`
# porque o ENTRYPOINT chama `dotnet X.dll` — o executável nativo seria peso morto.
RUN dotnet publish src/Explanando.Api/Explanando.Api.csproj \
        --configuration Release --no-restore -a $TARGETARCH --no-self-contained \
        -p:UseAppHost=false --output /publicado/api \
 && dotnet publish src/Explanando.Worker/Explanando.Worker.csproj \
        --configuration Release --no-restore -a $TARGETARCH --no-self-contained \
        -p:UseAppHost=false --output /publicado/worker

# ── Base de execução ─────────────────────────────────────────────────────────────────────
#
# Esta sim é da arquitetura de destino: sem `--platform`, o Docker usa `$TARGETPLATFORM`.
#
# `noble-chiseled` — sem gerenciador de pacotes, sem shell, sem ICU. Cabe porque a solution
# roda com InvariantGlobalization=true, que é decisão da Etapa 00: o motor compara texto e
# não pode mudar de resultado conforme o locale da máquina. A imagem sem ICU não é economia
# de bytes por si — é a consequência coerente daquela decisão.
#
# `-extra` traz as bibliotecas de ICU-less globalization e as raízes de certificado que os
# clientes HTTPS da Câmara, do Senado e da BrasilAPI exigem.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS base
WORKDIR /app

# A imagem chiseled já roda como não-root (uid 64198). Declarado para ficar explícito:
# quem impõe isso somos nós, não a plataforma.
USER $APP_UID

ENV DOTNET_EnableDiagnostics=0

# ── API ──────────────────────────────────────────────────────────────────────────────────
FROM base AS api
COPY --from=publicacao /publicado/api ./

# `ASPNETCORE_HTTP_PORTS` aceita a porta sem shell, que a imagem chiseled não tem. A API
# também obedece a `PORT` (ver Program.cs), para plataformas que a injetam.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Explanando.Api.dll"]

# ── Worker ───────────────────────────────────────────────────────────────────────────────
#
# O worker não é serviço de pé: cada comando (`ciclo-mensal`, `coletar-senado`,
# `coletar-cadeiras`, `migrar`...) sobe, trabalha e sai. Na VPS quem dispara é o timer do
# systemd, com `docker compose run --rm worker <comando>` — ver deploy/README.md.
FROM base AS worker
COPY --from=publicacao /publicado/worker ./

ENTRYPOINT ["dotnet", "Explanando.Worker.dll"]
CMD ["ciclo-mensal"]
