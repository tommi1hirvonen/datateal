// AKS cluster and role assignment for node pool management.
// Targeted at resource group scope (deployed via the subscription-scope main.bicep).

@description('Name of the AKS cluster.')
param clusterName string

@description('Azure region.')
param location string = resourceGroup().location

@description('Name of the AKS-managed node resource group (created automatically by AKS).')
param nodeResourceGroupName string

@description('VM size for the system node pool.')
param systemNodePoolVmSize string = 'Standard_D2as_v5'

@description('Object ID of the principal that will manage node pools. Empty = skip role assignment.')
param apiPrincipalId string = ''

// ── Managed Identity ──────────────────────────────────────────────────────────

resource clusterManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${clusterName}-mi'
  location: location
}

resource systemPoolManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${clusterName}-systempool-mi'
  location: location
}

var managedIdentityOperatorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'f1a07417-d97a-45cb-824c-7a7467783830' // Managed Identity Operator
)

// This is needed to allow the cluster to assign the kubelet identity to the node pools.
resource managedIdentityOperatorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  // Deterministic GUID so re-deployments are idempotent.
  name: guid(systemPoolManagedIdentity.id, clusterManagedIdentity.id, managedIdentityOperatorRoleId)
  scope: systemPoolManagedIdentity
  properties: {
    roleDefinitionId: managedIdentityOperatorRoleId
    principalId: clusterManagedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}


// ── AKS cluster ───────────────────────────────────────────────────────────────

resource cluster 'Microsoft.ContainerService/managedClusters@2025-10-01' = {
  name: clusterName
  location: location
  sku: {
    name: 'Base'
    tier: 'Free'
  }
  kind: 'Base'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${clusterManagedIdentity.id}': {}
    }
  }
  properties: {
    kubernetesVersion: '1.33.7'
    dnsPrefix: '${clusterName}-dns'
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
    servicePrincipalProfile: {
      clientId: 'msi'
    }
    enableRBAC: true
    aadProfile: {
      managed: true
      enableAzureRBAC: true
    }
    disableLocalAccounts: true
    networkProfile: {
      // Azure CNI Overlay: no pre-provisioned VNet required.
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      loadBalancerSku: 'standard'
    }
    identityProfile: {
      kubeletidentity: {
        resourceId: systemPoolManagedIdentity.id
        clientId: systemPoolManagedIdentity.properties.clientId
        objectId: systemPoolManagedIdentity.properties.principalId
      }
    }
    securityProfile: {
      imageCleaner: {
        enabled: true
        intervalHours: 168
      }
      workloadIdentity: {
        enabled: true
      }
    }
    oidcIssuerProfile: {
      enabled: true
    }
  }
}

// ── Role assignment ───────────────────────────────────────────────────────────

// Grants the API principal "Azure Kubernetes Service Contributor" on the cluster
// resource, which allows creating, listing, and deleting agent pools via ARM.
var aksContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ed7f3fbd-7b88-4dd4-9017-9adb7ce333f8' // Azure Kubernetes Service Contributor Role
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
