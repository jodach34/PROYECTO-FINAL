<#
.SYNOPSIS
    Cierra el MVC y la API de Rescauta y libera sus puertos.

.DESCRIPTION
    Con el cierre automatico (ApiLocal se registra en ApplicationStopping) esto ya no
    hace falta en el caso normal: al hacer Ctrl+C en "dotnet run", la API se cae sola.

    Este script queda como red de seguridad para cuando el proceso se muere de forma
    anormal (la consola se cierra de golpe, se corta la luz, Visual Studio se cuelga y
    mata al padre sin propagating el evento) y deja la API ocupando el puerto.

    EL PROBLEMA QUE RESUELVE, y por que el fallo es desconcertante: si la API sobrevive
    viva, la siguiente vez que se hace "dotnet run" el puerto aparece ocupado. La
    comprobacion de "esta ya la API?" dice que si, no se relanza nada, y el MVC se conecta
    a la API VIEJA. Todo parece funcionar y no se ve ningun cambio nuevo. Por eso aqui se
    matan los dos procesos siempre, y no solo si el puerto esta ocupado.

    Solo toca procesos de ESTE proyecto: el filtro de carpeta sobre Rescauta.Api impide
    que se cierre la API de otro proyecto que tengas abierta en la misma maquina.

.PARAMETER Puertos
    Puertos a comprobar. Por defecto los dos del proyecto.

.EXAMPLE
    .\stop.ps1
    Cierra PROYECTO-FINAL y Rescauta.Api, y libera 5142 y 5266.

.EXAMPLE
    .\stop.ps1 -SoloPuertos
    No cierra procesos: solo informa de quien tiene los puertos, para diagnosticar.
#>
[CmdletBinding()]
param(
    [int[]]$Puertos = @(5142, 5266),
    [switch]$SoloPuertos
)

$ErrorActionPreference = 'SilentlyContinue'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$carpetaRescauta = Join-Path $raiz 'Rescauta'

# --- 1. Informar de quien tiene los puertos -------------------------------------
$ocupados = @()

foreach ($puerto in $Puertos) {
    $conexion = Get-NetTCPConnection -LocalPort $puerto -State Listen |
                Select-Object -First 1

    if ($conexion) {
        $proceso = Get-Process -Id $conexion.OwningProcess
        $ocupados += [pscustomobject]@{
            Puerto  = $puerto
            PID     = $conexion.OwningProcess
            Proceso = $proceso.ProcessName
        }
    }
}

if ($ocupados.Count -eq 0) {
    Write-Host "Puertos $($Puertos -join ', ') libres. No hay nada que cerrar." -ForegroundColor Green
} else {
    Write-Host "Puertos ocupados:" -ForegroundColor Yellow
    $ocupados | Format-Table -AutoSize | Out-String | Write-Host
}

if ($SoloPuertos) {
    return
}

# --- 2. Cerrar la API, solo si es de este proyecto ------------------------------
#    Rescauta.Api.exe, no "dotnet": con "dotnet run" el proceso que escucha el puerto es
#    el .exe hijo, y el host "dotnet" es otro proceso distinto. Por eso el nombre.
$apiCerrada = 0

foreach ($p in Get-Process -Name 'Rescauta.Api') {
    $ruta = $p.MainModule.FileName

    if ($ruta -and $ruta.StartsWith($carpetaRescauta, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "  Cerrando API  -> $($p.ProcessName) (PID $($p.Id))" -ForegroundColor DarkGray
        Stop-Process -Id $p.Id -Force
        $apiCerrada++
    }
    else {
        Write-Host "  OJO: hay un Rescauta.Api ajeno en $($ruta). No se toca." -ForegroundColor Yellow
    }
}

# --- 3. Cerrar el MVC ----------------------------------------------------------
#    Se cierra POR NOMBRE de proceso, no por puerto: si el MVC se movio de puerto al
#    arrancar (porque el de por defecto estaba ocupado), cerrarlo por puerto fallaria.
$mvcCerrado = 0

foreach ($p in Get-Process -Name 'PROYECTO-FINAL') {
    Write-Host "  Cerrando MVC  -> $($p.ProcessName) (PID $($p.Id))" -ForegroundColor DarkGray
    Stop-Process -Id $p.Id -Force
    $mvcCerrado++
}

# --- 4. Resumen ----------------------------------------------------------------
Write-Host ""
Write-Host "Cerrados: $mvcCerrado MVC, $apiCerrada API." -ForegroundColor Green

# Los procesos "dotnet" que envuelven a un "dotnet run" no tienen puerto y no molestan
# al arrancar, asi que no se tocan: matarlos cerraria tambien los de Visual Studio.
Write-Host "Listo. Ya puedes volver a hacer 'dotnet run'." -ForegroundColor Green
