# Task Manager — Microservices Demo

A small but complete **microservices** system built with **ASP.NET Core 8**, containerized with
**Docker**, orchestrated with **Docker Compose**, and deployable to **Kubernetes**.

Three independent services communicate over REST:

```
                         ┌─────────────────────┐
                         │     Client / curl    │
                         │   (Swagger UI, etc.) │
                         └──────────┬───────────┘
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │     TaskService      │  :5002
                         │  (create/track tasks)│
                         └──────┬───────┬───────┘
                    validates   │       │  notifies
                    assignee    │       │
                                ▼       ▼
                  ┌──────────────┐   ┌──────────────────────┐
                  │  UserService │   │  NotificationService  │
                  │   :5001      │   │        :5003          │
                  └──────────────┘   └──────────────────────┘
```

- **UserService** — owns user data. CRUD API for users.
- **TaskService** — owns task data. When a task is created, it calls UserService to verify the
  assignee exists, then calls NotificationService to fire off a notification. This is the part
  that actually demonstrates "microservices" rather than just "three APIs."
- **NotificationService** — receives and stores notification events from other services.

Each service is independently buildable, deployable, and scalable — the core idea behind a
microservices architecture, as opposed to one monolithic API.

---

## Tech Stack

| Concern | Technology |
|---|---|
| Language / Framework | C#, ASP.NET Core 8 (Minimal APIs) |
| Inter-service communication | REST over HTTP (`IHttpClientFactory`) |
| API documentation | Swagger / OpenAPI (Swashbuckle) |
| Containerization | Docker (multi-stage builds) |
| Orchestration (local) | Docker Compose |
| Orchestration (cluster) | Kubernetes (Deployment + Service manifests) |
| Data storage | In-memory (see "Extending this project" to swap in a real DB) |

---

## Project Structure

```
microservices-taskmanager/
├── UserService/
│   ├── Program.cs
│   ├── UserService.csproj
│   └── Dockerfile
├── TaskService/
│   ├── Program.cs
│   ├── TaskService.csproj
│   └── Dockerfile
├── NotificationService/
│   ├── Program.cs
│   ├── NotificationService.csproj
│   └── Dockerfile
├── k8s/
│   ├── user-service.yaml
│   ├── task-service.yaml
│   └── notification-service.yaml
├── docker-compose.yml
└── README.md
```

---

## Running Locally (without Docker)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

Open 3 terminals:

```bash
# Terminal 1
cd UserService && dotnet run --urls=http://localhost:5001

# Terminal 2
cd NotificationService && dotnet run --urls=http://localhost:5003

# Terminal 3
cd TaskService && dotnet run --urls=http://localhost:5002
```

Swagger UI for each service is available at `http://localhost:<port>/swagger`.

---

## Running with Docker Compose (recommended)

Requires [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```bash
docker compose up --build
```

This builds all three images and starts them on a shared bridge network. Services are reachable at:

| Service | URL |
|---|---|
| UserService | http://localhost:5001/swagger |
| TaskService | http://localhost:5002/swagger |
| NotificationService | http://localhost:5003/swagger |

Stop everything with:

```bash
docker compose down
```

---

## Trying It Out (curl)

```bash
# 1. List seeded users (two demo users are seeded on UserService startup)
curl http://localhost:5001/api/users

# Copy one of the returned "id" values, then:

# 2. Create a task assigned to that user
curl -X POST http://localhost:5002/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"title":"Fix login bug","description":"NullRef on empty password","assignedUserId":"<PASTE_USER_ID>"}'

# 3. Update the task's status (copy the task "id" from the response above)
curl -X PUT http://localhost:5002/api/tasks/<TASK_ID>/status \
  -H "Content-Type: application/json" \
  -d '{"status":"InProgress"}'

# 4. Confirm NotificationService actually received the notifications
curl http://localhost:5003/api/notifications
```

Step 4 is the proof that the services are really talking to each other over the network, not just
sharing code in one process.

---

## API Reference

### UserService (`:5001`)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/users` | List all users |
| GET | `/api/users/{id}` | Get a user by id |
| POST | `/api/users` | Create a user |
| PUT | `/api/users/{id}` | Update a user |
| DELETE | `/api/users/{id}` | Delete a user |
| GET | `/health` | Health check |

### TaskService (`:5002`)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/tasks` | List all tasks |
| GET | `/api/tasks/{id}` | Get a task by id |
| POST | `/api/tasks` | Create a task (validates assignee via UserService, notifies via NotificationService) |
| PUT | `/api/tasks/{id}/status` | Update task status (`Pending`, `InProgress`, `Completed`) |
| DELETE | `/api/tasks/{id}` | Delete a task |
| GET | `/health` | Health check |

### NotificationService (`:5003`)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/notifications` | List all notifications |
| GET | `/api/notifications/user/{userId}` | List notifications for a specific user |
| POST | `/api/notifications` | Create a notification |
| GET | `/health` | Health check |

---

## Deploying to Kubernetes

Requires a local cluster (e.g. `minikube start` or Docker Desktop's Kubernetes) and `kubectl`.

```bash
# Build images and make them available to the cluster (minikube example)
eval $(minikube docker-env)
docker build -t taskmanager/user-service:latest ./UserService
docker build -t taskmanager/task-service:latest ./TaskService
docker build -t taskmanager/notification-service:latest ./NotificationService

# Apply manifests
kubectl apply -f k8s/user-service.yaml
kubectl apply -f k8s/notification-service.yaml
kubectl apply -f k8s/task-service.yaml

# Check pods
kubectl get pods

# TaskService is exposed via NodePort 30080
minikube service task-service --url
```

Each manifest defines a `Deployment` (2 replicas, liveness/readiness probes hitting `/health`) and
a `Service` for internal DNS-based discovery (`http://user-service:8080` etc.), matching the same
service names used in `docker-compose.yml`.

---

## Design Notes (worth mentioning in an interview)

- **Loose coupling**: TaskService's notification call is wrapped in try/catch and doesn't fail
  task creation if NotificationService is temporarily down — a basic resilience pattern.
- **Service discovery**: in Docker Compose, services find each other by container name over the
  bridge network; in Kubernetes, by Service name via cluster DNS. The application code doesn't
  hardcode IPs — URLs come from configuration/environment variables (`Services:UserService`).
  This is a simplified version of the same pattern used by tools like Consul or Eureka.
- **Health checks**: every service exposes `/health`, used by Docker Compose's `healthcheck` and
  Kubernetes' liveness/readiness probes — this is how orchestrators know a container is actually
  ready to receive traffic, not just "started."
- **Independent deployability**: each service has its own Dockerfile and can be built, versioned,
  and scaled independently (see `replicas: 2` in the k8s manifests).

---

## Extending This Project (good "future work" talking points)

- Replace the in-memory dictionaries with **Entity Framework Core + PostgreSQL/SQL Server** (one
  database per service, per microservices best practice).
- Add **JWT authentication** and an API Gateway (e.g. YARP or Ocelot) in front of all three services.
- Replace synchronous REST calls with an **async message queue** (RabbitMQ/Kafka) for notifications,
  so TaskService doesn't need NotificationService to be online at all.
- Add a **CI/CD pipeline** (GitHub Actions or Jenkins) that builds, tests, and pushes Docker images
  on every commit — ties directly back to the DevOps tools listed in most fresher job descriptions.
- Add centralized logging/tracing (e.g. Serilog + Seq, or OpenTelemetry) since debugging across
  three separate services is the real challenge in production microservices.

---

## Resume Bullet Points (based on this project)

- Designed and built a 3-service microservices system (ASP.NET Core 8) with REST-based
  inter-service communication, health checks, and independent deployability.
- Containerized each service with Docker (multi-stage builds) and orchestrated the full stack
  with Docker Compose, including service dependencies and health-check-gated startup.
- Wrote Kubernetes Deployment and Service manifests with liveness/readiness probes and horizontal
  scaling (2 replicas per service).
- Implemented resilient service-to-service calls (timeout handling, graceful degradation when a
  dependent service is unavailable).
