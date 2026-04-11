[← Back to Content](../README.md)

# Chapter 04 — Stateful Services with Orleans

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- What Microsoft Orleans is and when to use it
- Grains, silos, and the virtual actor model
- Deploying a single Orleans silo to Azure Container Apps
- Health probes: startup, liveness, and readiness
- Controlling container startup ordering with `dependsOn`

## Lab

In the lab for this chapter you will deploy the Orleans cluster container (single silo) to ACA, wire it up to the Game API, and verify that game state is maintained in Orleans grain memory across API requests.
