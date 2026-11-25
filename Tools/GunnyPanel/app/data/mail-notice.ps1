param(
    [Parameter(Mandatory = $true)][int]$UserId,
    [Parameter(Mandatory = $true)][int]$ZoneId
)

try {
    $url = 'http://127.0.0.1:4109/CenterServicePhP/'
    $soap = '<?xml version="1.0" encoding="utf-8"?>' +
            '<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">' +
            '<soap:Body><MailNotice xmlns="http://tempuri.org/">' +
            "<playerID>$UserId</playerID><zoneId>$ZoneId</zoneId>" +
            '</MailNotice></soap:Body></soap:Envelope>'

    $resp = Invoke-WebRequest -Uri $url -Method POST `
        -ContentType 'text/xml; charset=utf-8' `
        -Headers @{ SOAPAction = '"http://tempuri.org/ICenterService/MailNotice"' } `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($soap)) `
        -TimeoutSec 8 -UseBasicParsing

    if ($resp.Content -match 'MailNoticeResult>true<') { 'True' }
    else { 'False' }
}
catch {
    'False'
}
