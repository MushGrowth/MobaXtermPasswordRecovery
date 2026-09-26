param([string]$AssemblyPath = "$PSScriptRoot\..\bin\Release\net8.0-windows\win-x64\MobaXtermPasswordRecovery.dll")
$ErrorActionPreference = 'Stop'
# Synthetic fixtures only. Never load real configuration in regression tests.
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))
$method = $assembly.GetType('MobaXtermPasswordRecovery.Softwares.MobaXterm').GetMethod('DecryptMasterCiphertext', [Reflection.BindingFlags]'Static,NonPublic')
$aes = [Security.Cryptography.Aes]::Create()
$key = [byte[]](0..31)
$aes.Key = $key
$oldIV = $aes.EncryptEcb([byte[]]::new(16), [Security.Cryptography.PaddingMode]::None)
$salt = 'AbCdEf0123456789+X'
$newIV = [Text.Encoding]::ASCII.GetBytes($salt.Substring(0,16))
$passed = 0
foreach ($plain in @('a', 'synthetic-password-123!', '中文密码-测试', '  spaces  ', ('x' * 64))) {
    foreach ($newFormat in @($false, $true)) {
        $iv = if ($newFormat) { $newIV } else { $oldIV }
        $ct = $aes.EncryptCfb([Text.Encoding]::UTF8.GetBytes($plain), $iv, [Security.Cryptography.PaddingMode]::None, 8)
        $record = [Convert]::ToBase64String($ct)
        if ($newFormat) { $record = '_@' + $salt + $record }
        $actual = $method.Invoke($null, [object[]]@($record, $key))
        if ($actual -cne $plain) { throw 'Synthetic round-trip mismatch.' }
        $passed++
    }
}
foreach ($record in @('_@', '_@short', ('_@' + ('!' * 18) + 'AAAA'), ('_@' + $salt + '???'), '???')) {
    $rejected = $false
    try { $null = $method.Invoke($null, [object[]]@($record, $key)) } catch { $rejected = $true }
    if (!$rejected) { throw 'Malformed record was accepted.' }
    $passed++
}
$aes.Dispose()
"Synthetic tests passed: $passed"
