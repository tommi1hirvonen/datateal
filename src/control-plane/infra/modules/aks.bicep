// AKS cluster and role assignment for node pool management.
// Targeted at resource group scope (deployed via the subscription-scope main.bicep).

@description('Name of the AKS cluster.')
param clusterName string

@description('Azure region.')
param location string = resourceGroup().location

@description('Name of the AKS-managed node resource group (created automatically by AKS).')
param nodeResourceGroupName string

@description('VM size for the system node pool.')
param systemNodePoolVmSize string = 'Standard_D4as_v5'

@description('Object ID of the principal that will manage node pools. Empty = skip role assignment.')
param apiPrincipalId string = ''

// ── AKS cluster ───────────────────────────────────────────────────────────────

resource cluster 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: clusterName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    dnsPrefix: clusterName

    // AKS will create this resource group automatically to hold the node VMs,
    // NICs, public IPs, disks, etc. We just give it a predictable name.
    nodeResourceGroup: nodeResourceGroupName

    agentPoolProfiles: [
      {
        name: 'system'
        count: 1
        vmSize: systemNodePoolVmSize
        osType: 'Linux'
        osSKU: 'Ubuntu'
        mode: 'System'
        type: 'VirtualMachineScaleSets'
        enableAutoScaling: false
      }
    ]

    networkProfile: {
      // Azure CNI Overlay: no pre-provisioned VNet required.
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
    }
  }
}

// ── Role assignment ───────────────────────────────────────────────────────────

// Grants the API principal "Azure Kubernetes Service Contributor" on the cluster
// resource, which allows creating, listing, and deleting agent pools via ARM.
var aksContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ed7f3fbd-7b88-4dd4-9017-9adb7ce333f6' // Azure Kubernetes Service Contributor Role
)

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(apiPrincipalId)) {
  // Deterministic GUID so re-deployments are idempotent.
  name: guid(cluster.id, apiPrincipalId, aksContributorRoleId)
  scope: cluster
  properties: {
    roleDefinitionId: aksContributorRoleId
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output clusterName string = cluster.name
output clusterFqdn string = cluster.properties.fqdn
output kubeletIdentityObjectId string = cluster.properties.identityProfile.kubeletidentity.objectId
