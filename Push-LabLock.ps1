<#
.SYNOPSIS
    Copies LabLock to every PC in LabLock-computers.csv and runs it there without a window.

.DESCRIPTION
    EXPERIMENTAL - try it on one PC first (use -Only).

    Before you use it:
      1. On every lab PC, LabLock's setting "Allow remote management from the teacher PC" has been
         applied once (with the USB stick), and the PC is in the record (LabLock-computers.csv).
      2. Every lab PC has the same administrator account with the same password.
      3. Open LabLock once on this PC, tick the settings you want to push and close it.
         The ticks are stored in LabLock.ini, and that file is what the lab PCs receive.

    Run it from PowerShell opened as administrator, in the folder that holds LabLock.exe:

        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1
        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1 -Only 192.168.2.21
        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1 -Undo
        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1 -Restore -Only 192.168.2.21
        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1 -Wake

    -Wake only switches the PCs on (Wake-on-LAN), with the MAC addresses of the record. It needs no
    password; the PCs must have had "Prepare this PC for Wake-on-LAN" applied and Wake-on-LAN on in the BIOS.
        powershell -ExecutionPolicy Bypass -File .\Push-LabLock.ps1 -Restart

    The fixed IP address and the computer name are never pushed: they differ per PC.
    -Undo takes back the settings ticked in LabLock.ini. -Restore takes back everything LabLock has
    recorded on each PC, whatever is ticked, except the fixed IP address and remote management
    (the connection needs them; restore those at the PC itself with "Restore this PC...").
#>
[CmdletBinding()]
param(
    [switch]$Undo,
    [switch]$Restore,
    [switch]$Wake,
    [switch]$Restart,
    [string[]]$Only,
    [string]$AdminUser
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$exe = Join-Path $here 'LabLock.exe'
$csv = Join-Path $here 'LabLock-computers.csv'
if (-not (Test-Path $exe)) { throw "LabLock.exe was not found in $here" }
if (-not (Test-Path $csv)) { throw "LabLock-computers.csv was not found in $here. Run LabLock on the lab PCs first." }

# The record uses ';' or ',' between columns, depending on the Windows language it was made on.
$firstLine = Get-Content $csv -TotalCount 1 -Encoding UTF8
$delimiter = ','
if ($firstLine -like '*;*') { $delimiter = ';' }

$pcs = @(Import-Csv $csv -Delimiter $delimiter -Encoding UTF8 | Where-Object { $_.'IP address' })
if ($Only) {
    $pcs = @($pcs | Where-Object { ($Only -contains $_.'IP address') -or ($Only -contains $_.'Computer name') })
}
if ($pcs.Count -eq 0) { throw 'No PC with an IP address was found in the record.' }

if ($Wake) {
    $client = New-Object System.Net.Sockets.UdpClient
    $client.EnableBroadcast = $true
    $woken = 0
    foreach ($pc in $pcs) {
        $hex = ($pc.'MAC address' -replace '[-:\.]', '')
        if ($hex.Length -ne 12) { Write-Host ("   no MAC address: " + $pc.'Computer name') -ForegroundColor Yellow; continue }
        $mac = [byte[]]@(0..5 | ForEach-Object { [Convert]::ToByte($hex.Substring($_ * 2, 2), 16) })
        $packet = [byte[]](@(0xFF) * 6 + ($mac * 16))
        $octets = $pc.'IP address'.Split('.')
        $targets = @('255.255.255.255', ($octets[0..2] -join '.') + '.255')
        foreach ($round in 1..3) {
            foreach ($target in $targets) { [void]$client.Send($packet, $packet.Length, $target, 9) }
        }
        Write-Host ("   wake-up signal sent to {0} ({1})" -f $pc.'Computer name', $pc.'MAC address') -ForegroundColor Green
        $woken++
        Start-Sleep -Milliseconds 150
    }
    $client.Close()
    Write-Host ''
    Write-Host "Sent to $woken PCs. They need a minute or two to start." -ForegroundColor Cyan
    return
}

# The administrator account name comes from LabLock.ini unless you give -AdminUser.
if (-not $AdminUser) {
    $AdminUser = 'LabAdmin'
    $ini = Join-Path $here 'LabLock.ini'
    if (Test-Path $ini) {
        $line = Get-Content $ini -Encoding UTF8 | Where-Object { $_ -like 'admin=*' } | Select-Object -First 1
        if ($line -and $line.Length -gt 6) { $AdminUser = $line.Substring(6).Trim() }
    }
}
$credential = Get-Credential -UserName $AdminUser -Message 'Administrator account of the lab PCs'
if (-not $credential) { throw 'No password was given.' }

# PCs that are not in a domain must be listed as trusted before PowerShell will talk to them.
$addresses = @($pcs | ForEach-Object { $_.'IP address' })
$trusted = (Get-Item WSMan:\localhost\Client\TrustedHosts).Value
if ($trusted -ne '*') {
    $all = @(@($trusted -split ',') + $addresses | Where-Object { $_ } | Select-Object -Unique)
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value ($all -join ',') -Force
}

$files = @('LabLock.exe', 'LabLock.ini', 'LabLock-blocklist.txt', 'LabLock-school.png' |
    ForEach-Object { Join-Path $here $_ } | Where-Object { Test-Path $_ })

$arguments = @('/apply')
if ($Undo) { $arguments = @('/undo') }
if ($Restore) { $arguments = @('/restore') }
if ($Restart) { $arguments += '/restart' }

$ok = 0
$bad = 0
foreach ($pc in $pcs) {
    $address = $pc.'IP address'
    Write-Host ''
    Write-Host ("== {0}  ({1})" -f $pc.'Computer name', $address) -ForegroundColor Cyan
    $session = $null
    try {
        $session = New-PSSession -ComputerName $address -Credential $credential
        Invoke-Command -Session $session -ScriptBlock { New-Item -ItemType Directory -Force -Path 'C:\LabLock' | Out-Null }
        foreach ($file in $files) {
            Copy-Item -Path $file -Destination 'C:\LabLock\' -ToSession $session -Force
        }

        $code = Invoke-Command -Session $session -ArgumentList (, $arguments) -ScriptBlock {
            param($lablockArguments)
            $process = Start-Process -FilePath 'C:\LabLock\LabLock.exe' -ArgumentList $lablockArguments -Wait -PassThru
            $process.ExitCode
        }

        # the last lines of that PC's log
        Invoke-Command -Session $session -ScriptBlock {
            if (Test-Path 'C:\LabLock\LabLock-log.txt') { Get-Content 'C:\LabLock\LabLock-log.txt' -Tail 30 -Encoding UTF8 }
        } | ForEach-Object { Write-Host "   $_" }

        if ($code -eq 0) {
            Write-Host '   Done.' -ForegroundColor Green
            $ok++
        }
        else {
            Write-Host "   Finished with problems (exit code $code). Read the lines above." -ForegroundColor Yellow
            $bad++
        }
    }
    catch {
        Write-Host ("   FAILED: " + $_.Exception.Message) -ForegroundColor Red
        $bad++
    }
    finally {
        if ($session) { Remove-PSSession $session }
    }
}

Write-Host ''
Write-Host "$ok PCs done, $bad with problems."
