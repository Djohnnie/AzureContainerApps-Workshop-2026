[← Back to Content](../README.md)

# Chapter 03 — Multi-Container Apps & Service Discovery

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- Internal ingress and the ACA DNS naming convention
- Service-to-service communication without a load balancer
- Adding a Minimal API container alongside the frontend
- Dapr sidecar basics: the Dapr HTTP/gRPC building block
- Environment-level networking and security boundaries

## Lab

In the lab for this chapter you will add the Game API container (ASP.NET Minimal API + SignalR Hub) to the ACA environment, configure internal-only ingress, and update the frontend to route API calls through the ACA service name.
