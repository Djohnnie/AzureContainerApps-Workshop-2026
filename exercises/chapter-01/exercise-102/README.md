[← Back to Exercises](../../README.md) · [↑ View Slide](../../../content/chapter-01/README.md#slide-07--exercise-102-your-first-web-app-in-a-container)

# Exercise 102

## Your first ASP.NET web app in a container

Create a .NET 10 minimal ASP.NET web application that serves a simple HTML page displaying a Hello World message rendered as ASCII art (using Spectre.Console's FigletText).

---

> ### 💬 Copilot Prompt
>
> ```
> Create a .NET 10 empty web app that just returns a simple HTML page (using minimal
> ASP.NET code) that contains Hello World in ASCII art like what Spectre.Console does.
> The project should be named AzureContainerApps.Exercise102.
> Also create a Dockerfile to be able to build and run it.
> ```

---

## Running with Docker Desktop

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### 1. Build the image

Open a terminal in the `AzureContainerApps.Exercise102` project folder and run:

```bash
docker build -t azure-container-apps-exercise-102:latest .
```

| Flag | Description |
|------|-------------|
| `-t azure-container-apps-exercise-102:latest` | Tags the built image with a name so you can reference it by name instead of its hash |

This uses the multi-stage `Dockerfile` to restore, compile and publish the app inside an SDK container, then copies only the published output into a lean ASP.NET runtime image.

### 2. Run the container

```bash
docker run --rm -p 8080:8080 azure-container-apps-exercise-102:latest
```

| Flag | Description |
|------|-------------|
| `--rm` | Automatically removes the container after it exits |
| `-p 8080:8080` | Maps port 8080 on your machine to port 8080 inside the container |

Open [http://localhost:8080](http://localhost:8080) in your browser to see the Hello World ASCII art page.

### 3. Verify in Docker Desktop

Open **Docker Desktop → Images** to confirm the `azure-container-apps-exercise-102` image was built, or run:

```bash
docker images azure-container-apps-exercise-102
```

---

## Running with Docker Compose

The included `docker-compose.yml` pre-configures the image tag and port mapping, so you don't need to pass them manually.

### 1. Build the image

```bash
docker compose build
```

### 2. Run the container

```bash
docker compose up
```

Open [http://localhost:8080](http://localhost:8080) in your browser to see the Hello World ASCII art page.
