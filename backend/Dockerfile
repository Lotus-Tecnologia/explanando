# Imagem da API e do worker, num Dockerfile só.
#
# Um arquivo com dois alvos, e não dois arquivos: as camadas de restore e build são idênticas
# — mesma solution, mesmos pacotes — e separá-las faria o cache do Docker reconstruir tudo
# duas vezes por deploy.
#
# Constrói com:
#   docker build -f backend/Dockerfile --target api    -t explanando-api    backend
#   docker build -f backend/Dockerfile --target worker -t explanando-worker backend

# ── Restauração ──────────────────────────────────────────────────────────────────────────
#
# Copia só os arquivos de projeto antes do código. É o que faz o `restore` — a parte lenta —
# reaproveitar cache enquanto nenhuma dependência mudar. Copiar tudo de uma vez invalidaria
# essa camada a cada linha de C# alterada.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS restauracao
WORKDIR /origem

# O .editorconfig vem junto, e não é detalhe de estilo: é ele que marca
# `src/**/Migrations/*.cs` como `generated_code = true`. Sem ele aqui dentro, os analisadores
# reprovam o código que o próprio EF Core gerou (CA1861/CA1825) e, com
# TreatWarningsAsErrors, o build do contêiner falha enquanto o local passa.
COPY Explanando.slnx Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/ .config/
COPY src/Explanando.Domain/*.csproj          src/Explanando.Domain/
COPY src/Explanando.Application/*.csproj     src/Explanando.Application/
COPY src/Explanando.Infrastructure/*.csproj  src/Explanando.Infrastructure/
COPY src/Explanando.Api/*.csproj             src/Explanando.Api/
COPY src/Explanando.Worker/*.csproj          src/Explanando.Worker/
COPY tests/Explanando.Domain.Tests/*.csproj        tests/Explanando.Domain.Tests/
COPY tests/Explanando.Application.Tests/*.csproj   tests/Explanando.Application.Tests/
COPY tests/Explanando.Integration.Tests/*.csproj   tests/Explanando.Integration.Tests/
COPY tests/Explanando.E2E.Tests/*.csproj           tests/Explanando.E2E.Tests/
COPY tests/Explanando.Architecture.Tests/*.csproj  tests/Explanando.Architecture.Tests/

RUN dotnet restore Explanando.slnx

# ── Publicação ───────────────────────────────────────────────────────────────────────────
FROM restauracao AS publicacao
COPY src/ src/

# `--no-restore` porque a camada acima já restaurou. Sem ele, o Docker faria a rede de novo
# a cada build, jogando fora exatamente o cache que a separação existe para criar.
RUN dotnet publish src/Explanando.Api/Explanando.Api.csproj \
        --configuration Release --no-restore --output /publicado/api \
 && dotnet publish src/Explanando.Worker/Explanando.Worker.csproj \
        --configuration Release --no-restore --output /publicado/worker

# ── Base de execução ─────────────────────────────────────────────────────────────────────
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

# A imagem chiseled já roda como não-root (uid 64198). Declarado para ficar explícito, e
# porque o Cloud Run não impõe isso — quem impõe somos nós.
USER $APP_UID

ENV DOTNET_EnableDiagnostics=0

# ── API ──────────────────────────────────────────────────────────────────────────────────
FROM base AS api
COPY --from=publicacao /publicado/api ./

# O Cloud Run injeta PORT e espera o contêiner escutando nela — 8080 é só o padrão de quando
# a variável não vem. `ASPNETCORE_HTTP_PORTS` aceita a substituição sem shell, que a imagem
# chiseled não tem.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Explanando.Api.dll"]

# ── Worker ───────────────────────────────────────────────────────────────────────────────
#
# Sem comando fixo: o worker tem vários (`ciclo-mensal`, `coletar`, `varrer`, `consolidar`),
# e quem escolhe é o Cloud Run Job ou o agendador. `servico` fica de fora do padrão de
# propósito — um contêiner de pé esperando um mês passar contradiz o scale-to-zero.
FROM base AS worker
COPY --from=publicacao /publicado/worker ./

ENTRYPOINT ["dotnet", "Explanando.Worker.dll"]
CMD ["ciclo-mensal"]
