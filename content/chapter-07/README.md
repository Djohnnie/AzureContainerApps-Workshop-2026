[← Back to Content](../README.md)

# Chapter 07 — Secrets & Managed Identity

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- Why hard-coded secrets in container images are dangerous
- Azure Key Vault: vaults, secrets, access policies
- Managed Identity: system-assigned vs user-assigned
- ACA secret references and Key Vault integration
- Rotating secrets without redeploying containers

## Lab

In the lab for this chapter you will move all connection strings and API keys out of environment variables and into Azure Key Vault, grant the ACA containers access via Managed Identity, and verify the app still works without any secrets in the deployment manifest.
