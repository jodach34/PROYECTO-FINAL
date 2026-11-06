<#
.SYNOPSIS
    Levanta la API de Rescauta (Punto 1, Clean Architecture).

.DESCRIPTION
    El repositorio tiene DOS aplicaciones y por eso 'dotnet run' a secas levanta la
    equivocada:

      1. PROYECTO-FINAL.csproj (raiz, net10.0)  -> MVC legacy con las 3 paginas estaticas
      2. Rescauta/Rescauta.Api (net8.0)          -> API del Punto 1, Clean Architecture

    Son independientes: el proyecto raiz no referencia a Rescauta. Este script deja
    explicito cual se levanta, para no tener que acordarse del --project.

.EXAMPLE
    .\start.ps1
    Arranca la API en http://localhost:5277

.EXAMPLE
    .\start.ps1 -Vistas
    Arranca el MVC legacy con las paginas estaticas, en http://localhost:5099

.EXAMPLE
    .\start.ps1 -Puert 6000
    Arranca la API en otro puerto
#>
[CmdletBinding()]
param(
    [switch]$Vistas,
    [int]$Puert = 5277
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($Vistas) {
    $proyecto = Join-Path $raiz 'PROYECTO-FINAL.csproj'
    Write-Host "Arrancando el MVC legacy (3 paginas estaticas) en http://localhost:$Puert" -ForegroundColor Yellow
} else {
    $proyecto = Join-Path $raiz 'Rescauta\Rescauta.Api\Rescauta.Api.csproj'
    Write-Host "Arrancando la API de Rescauta (Punto 1) en http://localhost:$Puert" -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $proyecto)) {
    throw "No se encontro el proyecto: $proyecto"
}

Write-Host "Swagger: http://localhost:$Puert/swagger`n" -ForegroundColor DarkGray

& dotnet run --project $proyecto --urls "http://localhost:$Puert"
