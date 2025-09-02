@description('The name of the SQL logical server.')
param serverName string = 'wiley-widget-sql-${uniqueString(resourceGroup().id)}'

@description('The name of the SQL Database.')
param databaseName string = 'WileyWidgetDb'

@description('The administrator username of the SQL logical server.')
@secure()
param administratorLogin string

@description('The administrator password of the SQL logical server.')
@secure()
param administratorLoginPassword string

@description('Location for all resources.')
param location string = resourceGroup().location

@description('The pricing tier for the SQL Database.')
param skuName string = 'S0'

@description('The maximum size of the SQL Database in GB.')
param maxSizeBytes int = 268435456000

@description('The collation of the SQL Database.')
param collation string = 'SQL_Latin1_General_CP1_CI_AS'

resource sqlServer 'Microsoft.Sql/servers@2022-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    collation: collation
    maxSizeBytes: maxSizeBytes
    zoneRedundant: false
    licenseType: 'LicenseIncluded'
    readScale: 'Disabled'
  }
}

resource firewallRule 'Microsoft.Sql/servers/firewallRules@2022-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
