// Deploys the full DuckHouse Control Plane AKS infrastructure at resource group scope.
//
// One additional resource group is created:
//   • nodeResourceGroupName – AKS creates it automatically to
//                             hold the underlying VMs, NICs, disks, etc.
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
param clusterName string = 'aks-duckhouse-dev'

@description('Name of the Azure Container Registry. Must be globally unique and alphanumeric only.')
param acrName string

@description('Name of the AKS-managed node resource group (created automatically by AKS).')
param nodeResourceGroupName string = 'mrg-duckhouse-dev'

@description('VM size for the required system node pool.')
param systemNodePoolVmSize string = 'Standard_D2as_v5'

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
  }
}

// ── Outputs (use these to configure NodeService:Aks in appsettings) ───────────

output subscriptionId string = subscription().subscriptionId
output resourceGroupName string = resourceGroup().name
output clusterName string = aks.outputs.clusterName
output nodeResourceGroupName string = nodeResourceGroupName
output acrLoginServer string = aks.outputs.acrLoginServer
