$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '..\Assets\Scripts\MaintenanceSession.cs')
$taskFlow = [Ps5Maintenance.MaintenanceSession]::new(10)
if ($taskFlow.Advance($false,$true,$true)) { throw 'Unconfirmed step advanced' }
if ($taskFlow.Advance($true,$false,$true)) { throw 'Lost tracking advanced' }
if ($taskFlow.Advance($true,$true,$false)) { throw 'Unknown model advanced' }
if ($taskFlow.Back()) { throw 'Back before first step' }
if (!$taskFlow.Advance($true,$true,$true) -or $taskFlow.Index -ne 1) { throw 'First advance failed' }
if (!$taskFlow.Back() -or $taskFlow.Index -ne 0) { throw 'Back failed' }
for ($taskIndex = 0; $taskIndex -lt 10; $taskIndex++) {
    if (!$taskFlow.Advance($true,$true,$true)) { throw 'Valid advance failed' }
}
if (!$taskFlow.Finished -or ($taskFlow.Completed -contains $false)) { throw 'Completion failed' }
if ($taskFlow.Advance($true,$true,$true) -or $taskFlow.Back()) { throw 'Finished state changed' }
Write-Output 'PS5_XR_CSHARP_STATE_CHECKS_PASSED'
