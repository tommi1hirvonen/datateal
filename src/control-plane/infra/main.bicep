// Deploys the full DuckHouse Control Plane AKS infrastructure at subscription scope.
//
// Two resource groups are involved:
//   • resourceGroupName     – created here; holds the AKS cluster ARM resource.
//   • nodeResourceGroupName – NOT created here; AKS creates it automatically to
//                             hold the underlying VMs, NICs, disks, etc.
//
// Deploy with:
//   az deployment sub create \
//     --location westeurope \
//     --template-file main.bicep \
//     --parameters main.bicepparam

targetScope = 'subscription'

@description('Azure region for all resources.')
param location string = 'westeurope'

@description('Name of the AKS cluster.')
param clusterName string = 'aks-duckhouse-dev'

@description('Name of the resource group that will hold the AKS cluster resource.')
param resourceGroupName string = 'rg-duckhouse-dev'

@description('Name of the AKS-managed node resource group (created automatically by AKS).')
param nodeResourceGroupName string = 'mrg-duckhouse-dev'

@description('VM size for the required system node pool.')
param systemNodePoolVmSize string = 'Standard_D4as_v5'

@description('''
Object ID of the service principal or managed identity that will call the AKS ARM API
to manage node pools (i.e. the identity the Control Plane API runs under).
Leave empty to skip the role assignment and add it manually later.
''')
param apiPrincipalId string = ''

// ── Resource group ────────────────────────────────────────────────────────────

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

// ── AKS cluster ───────────────────────────────────────────────────────────────

module aks 'modules/aks.bicep' = {
  name: 'aks'
  scope: rg
  params: {
    clusterName: clusterName
    location: location
    nodeResourceGroupName: nodeResourceGroupName
    systemNodePoolVmSize: systemNodePoolVmSize
    apiPrincipalId: apiPrincipalId
  }
}

// ── Outputs (use these to configure NodeService:Aks in appsettings) ───────────

output subscriptionId string = subscription().subscriptionId
output resourceGroupName string = rg.name
output clusterName string = aks.outputs.clusterName
output nodeResourceGroupName string = nodeResourceGroupName
