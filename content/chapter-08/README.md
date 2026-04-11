[← Back to Content](../README.md)

# Chapter 08 — Custom KEDA Scalers

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- KEDA (Kubernetes Event-Driven Autoscaling) fundamentals
- Built-in KEDA scalers vs the External Scaler protocol
- The External Scaler gRPC contract (`IsActive`, `GetMetricSpec`, `GetMetrics`)
- Querying Orleans via `IManagementGrain` to expose custom metrics
- Wiring a custom scaler to an ACA scale rule

## Lab

In the lab for this chapter you will deploy the KEDA External Scaler container (ASP.NET gRPC service), implement the three required gRPC methods backed by Orleans management grain queries, and configure ACA to use it as the scale trigger for the Orleans cluster.
