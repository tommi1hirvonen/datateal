using './main.bicep'

param location = 'swedencentral'
param clusterName = 'aks-duckhouse-dev'
param acrName = 'acrduckhousedev' // Must be globally unique; change if the name is already taken
param nodeResourceGroupName = 'mrg-duckhouse-dev'
param systemNodePoolVmSize = 'Standard_D2as_v5'

// Set this to the object ID of the managed identity or service principal that
// the Control Plane API runs as, so it can create and delete node pools.
// You can retrieve it after deploying the API and re-run the deployment.
param apiPrincipalId = ''
