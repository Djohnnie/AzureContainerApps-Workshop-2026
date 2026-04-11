[← Back to Content](../README.md)

# Chapter 06 — Worker Services & Messaging

> 🚧 **Coming soon** — slides for this chapter are in preparation.

## What you'll learn

- .NET Worker Services as background containers in ACA
- Azure Cache for Redis: data structures, pub/sub channels
- The game tick loop: Worker → Orleans → Redis → SignalR
- Pub/sub patterns and fan-out at scale
- Connecting containerised workers to ACA-internal services

## Lab

In the lab for this chapter you will deploy the Game Tick Service worker container, connect it to Redis, and watch real-time game state flow from the worker through Redis pub/sub to connected browser clients via SignalR.
