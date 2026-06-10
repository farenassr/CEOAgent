# Azure Infrastructure

`main.bicep` is a deployable starter for the MVP Azure footprint. It avoids
secrets as parameters; runtime secrets should be created in Key Vault after
provisioning and referenced by `kv://` aliases or Key Vault secret URIs.

Validate locally:

```powershell
az bicep build --file infra/main.bicep
```
