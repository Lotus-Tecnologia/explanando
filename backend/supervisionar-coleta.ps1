<#
    ESTE ARQUIVO PRECISA DE BOM UTF-8.

    O Windows PowerShell 5.1 le .ps1 SEM BOM como ANSI. Os acentos destes comentarios viram
    bytes invalidos, e o parser falha longe da causa — a mensagem acusa "'}' de fechamento
    ausente" numa linha que esta correta. E' a mesma familia da armadilha do heredoc
    registrada na secao 3.5 do handoff: a ferramenta come um caractere e o erro aparece em
    outro lugar.

.SYNOPSIS
    Relanca a coleta quando ela para por degradacao da origem.

.DESCRIPTION
    A coleta sai com codigo 2 quando dez deputados seguidos voltam em exercicio e sem
    despesa — a defesa que nasceu do incidente de 2026-08-21, quando a rota /despesas da
    Camara respondeu HTTP 200 com lista vazia por horas. Parar e' o comportamento CERTO:
    insistir so' produz checkpoint ruim e carga numa API ja degradada.

    Mas parar as duas da manha e ficar parado ate' as nove desperdica a noite. Como o
    checkpoint torna a coleta retomavel, relancar depois de uma espera e' seguro: a proxima
    passada recomeca de onde parou.

    ESTE SCRIPT NAO EXISTE EM PRODUCAO. La', quem faz este papel e' a politica de retentativa
    do Cloud Run Job — e' o mesmo comportamento, declarado na plataforma em vez de num laco.

.EXAMPLE
    # Supervisiona uma coleta que ja esta rodando:
    .\supervisionar-coleta.ps1 -PidAtual 1388

    # Ou inicia do zero:
    .\supervisionar-coleta.ps1
#>
param(
    # PID de uma coleta ja em andamento, para nao iniciar uma segunda em paralelo.
    [int] $PidAtual = 0,

    # Quantas vezes relancar antes de desistir. Oito cobre uma noite inteira de
    # instabilidade intermitente; alem disso, o problema deixou de ser transitorio.
    [int] $MaximoDeTentativas = 8,

    # Espera antes de relancar. A degradacao de 2026-08-21 durou horas — tentar de novo em
    # trinta segundos so' repetiria o diagnostico e castigaria a origem.
    [int] $MinutosEntreTentativas = 15,

    [int] $Legislatura = 57
)

$ErrorActionPreference = "Stop"

$raiz = Split-Path -Parent $PSScriptRoot
$conexao = (Get-Content "$raiz\backend\.env.neon" -Raw).Trim()
$dll = "$env:LOCALAPPDATA\explanando\artifacts\bin\Explanando.Worker\release\Explanando.Worker.dll"
$diario = "$env:LOCALAPPDATA\explanando\supervisor.log"

function Anotar($mensagem) {
    $linha = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $mensagem
    Add-Content -Path $diario -Value $linha
    Write-Output $linha
}

function EstaInterrompida($caminhoDoLog) {
    if (-not (Test-Path $caminhoDoLog)) { return $true }
    $texto = Get-Content $caminhoDoLog -Raw
    # Sem o bloco de resumo, o processo morreu antes de terminar — tambem e' motivo para
    # tentar de novo.
    if ($texto -notmatch "=== COLETA ===") { return $true }
    return ($texto -match "INTERROMPIDA") -or ($texto -match "LISTAGEM INCOMPLETA")
}

$env:EXPLANANDO_CONEXAO = $conexao
Anotar "supervisor iniciado (maximo de $MaximoDeTentativas tentativas)"

# Se ja ha uma coleta rodando, espera por ela antes de considerar relancar.
$logAtual = "$env:LOCALAPPDATA\explanando\coleta-neon-2.log"

if ($PidAtual -gt 0) {
    $proc = Get-Process -Id $PidAtual -ErrorAction SilentlyContinue
    if ($proc) {
        Anotar "aguardando a coleta em andamento (PID $PidAtual)"
        $proc.WaitForExit()
        Anotar "a coleta em andamento terminou"
    }
}

for ($tentativa = 1; $tentativa -le $MaximoDeTentativas; $tentativa++) {
    if (-not (EstaInterrompida $logAtual)) {
        Anotar "COLETA COMPLETA — nada a relancar"
        exit 0
    }

    Anotar "coleta incompleta; esperando $MinutosEntreTentativas min antes da tentativa $tentativa"
    Start-Sleep -Seconds ($MinutosEntreTentativas * 60)

    $logAtual = "$env:LOCALAPPDATA\explanando\coleta-neon-r$tentativa.log"
    Anotar "relancando -> $logAtual"

    $p = Start-Process -FilePath "dotnet" `
        -ArgumentList "`"$dll`"", "coletar", "--legislatura", "$Legislatura" `
        -RedirectStandardOutput $logAtual `
        -RedirectStandardError "$logAtual.err" `
        -WindowStyle Hidden -PassThru

    $p.WaitForExit()
    Anotar "tentativa $tentativa terminou com codigo $($p.ExitCode)"
}

Anotar "DESISTINDO depois de $MaximoDeTentativas tentativas — a origem nao se recuperou"
exit 1
