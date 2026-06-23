// Deploys the full Datateal Control Plane AKS infrastructure at resource group scope.
//
// One additional resource group is created:
//   • nodeResourceGroupName – AKS creates it automatically to
//                             hold the underlying VMs, NICs, disks, etc.
//
// IMPORTANT!!! The managed node resource group must not exist before deployment.
// When AKS creates its own node resource group, it automatically applies necessary
// permissions and grants its own service principal the Contributor role on it.
// If the resource group already exists, AKS will not be able to set up the permissions
// correctly and the deployment will fail.
//
// Deploy with:
//   az deployment group create \
//     --resource-group <your-resource-group> \
//     --template-file main.bicep \
//     --parameters main.bicepparam

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = 'westeurope'

@description('Name of the AKS cluster.')
param clusterName string = 'aks-datateal-dev'

@description('Name of the Azure Container Registry. Must be globally unique and alphanumeric only.')
param acrName string

@description('Name of the PostgreSQL Flexible Server. Must be globally unique.')
param psqlName string = 'psql-aks-datateal-dev'

@description('Name of the storage account for ADLS Gen2. Must be globally unique and lowercase only.')
param storageAccountName string = 'stdatatealdev'

@description('Name of the AKS-managed node resource group (created automatically by AKS).')
param nodeResourceGroupName string = 'mrg-datateal-dev'

@description('VM size for the required system node pool.')
param systemNodePoolVmSize string = 'Standard_D2as_v5'

@description('IP address to be whitelisted to the PostgreSQL Flexible Server and ADLS')
param firewallWhitelistIp string = ''

@description('Admin password for the PostgreSQL Flexible Server')
@secure()
param postgresAdminPassword string

@description('''
Object ID of the service principal or managed identity that will call the AKS ARM API
to manage node pools (i.e. the identity the Control Plane API runs under).
Leave empty to skip the role assignment and add it manually later.
''')
param apiPrincipalId string = ''

// ── AKS cluster ───────────────────────────────────────────────────────────────

module aks 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    clusterName: clusterName
    location: location
    nodeResourceGroupName: nodeResourceGroupName
    systemNodePoolVmSize: systemNodePoolVmSize
    apiPrincipalId: apiPrincipalId
    acrName: acrName
    firewallWhitelistIp: firewallWhitelistIp
    postgresAdminPassword: postgresAdminPassword
    psqlName: psqlName
    storageAccountName: storageAccountName
  }
}

// ── Outputs (use these to configure NodeService:Aks in appsettings) ───────────

output subscriptionId string = subscription().subscriptionId
output resourceGroupName string = resourceGroup().name
output clusterName string = aks.outputs.clusterName
output nodeResourceGroupName string = nodeResourceGroupName
output acrLoginServer string = aks.outputs.acrLoginServer
output nodeSubnetId string = aks.outputs.nodeSubnetId
