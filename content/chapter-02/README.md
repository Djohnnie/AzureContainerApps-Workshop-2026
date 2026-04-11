[← Back to Content](../README.md)

# Chapter 02 — Multi-Container apps and scaling using replicas

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- Connecting multiple containers using internal ingress
- Configuring environment variables across container revisions
- Scaling Container Apps using replica count rules
- Scaling to zero and back — how ACA manages cold starts

## Lab

In the lab for this chapter you will extend the Snake application with a second container connected via internal ingress, pass configuration between them using environment variables, and configure replica-based scaling rules including scale-to-zero.

## Exercises

| # | Exercise | Description |
|---|----------|-------------|
| 1 | [Exercise 201 — Multi-Container App with internal API](../../exercises/chapter-02/exercise-201/README.md) | Connect a web front end and a minimal API back end using an environment variable and observe the API hostname from the web page |
