using './main.bicep'

param location = 'westeurope'
param clusterName = 'aks-duckhouse-dev'
param resourceGroupName = 'rg-lakehouse-dev'
param nodeResourceGroupName = 'mrg-lakehouse-dev'
param systemNodePoolVmSize = 'Standard_D4as_v5'

// Set this to the object ID of the managed identity or service principal that
// the Control Plane API runs as, so it can create and delete node pools.
// You can retrieve it after deploying the API and re-run the deployment.
param apiPrincipalId = ''
