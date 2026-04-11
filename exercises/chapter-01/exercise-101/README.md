[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-01/README.md#slide-06--exercise-101-your-first-container)

# Exercise 101

## Your first Docker container

Create a .NET 10 Console application that uses Spectre.Console to show a nice Hello World message in the terminal.

---

> ### 💬 Copilot Prompt
>
> ```
> Create a .NET 10 Console Application that uses Spectre.Console to show a nice
> Hello World message to the terminal and also create a Dockerfile to have this
> packaged in a container image that is runnable using Docker Desktop.
> ```

---

## Running with Docker Desktop

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build the image

Open a terminal in the `AzureContainerApps.Exercise101` project folder and run:

```bash
docker build -t azurecontainerapps-exercise101 .
```

| Flag | Description |
|------|-------------|
| `-t azurecontainerapps-exercise101` | Tags the built image with a name so you can reference it by name instead of its hash |

This uses the multi-stage `Dockerfile` to restore, compile and publish the app inside an SDK container, then copies only the published output into a lean runtime image.

### 2. Run the container

```bash
docker run --rm azurecontainerapps-exercise101
```

| Flag | Description |
|------|-------------|
| `--rm` | Automatically removes the container after it exits |

You should see the Spectre.Console Hello World output printed in your terminal.

### 3. Verify in Docker Desktop

Open **Docker Desktop → Images** to confirm the `azurecontainerapps-exercise101` image was built, or run:

```bash
docker images azurecontainerapps-exercise101
```

---

## Running with Docker Compose

The included `docker-compose.yml` pre-configures the image tag, so you don't need to pass `-t` manually.

### 1. Build the image

```bash
docker compose build
```

### 2. Run the container

```bash
docker compose up
```

You should see the Spectre.Console Hello World output printed in your terminal.
