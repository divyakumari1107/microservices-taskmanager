# Task Manager — Microservices

A production-style microservices-based Task Manager built with ASP.NET Core 8, Docker, Docker Compose and Kubernetes.

## 🚀 Overview

This project demonstrates a distributed task management system using independently deployable microservices.

The system contains:

- UserService — manages users
- TaskService — manages tasks
- NotificationService — handles notifications
- Kubernetes manifests for container orchestration
- Docker containerization for each service
- REST-based service communication
- Health checks for service monitoring

## 🏗️ Architecture

```mermaid
flowchart TD
    Client[Client / REST API]

    Client --> User[UserService]
    Client --> Task[TaskService]

    Task --> Notification[NotificationService]

    User --> DB1[(User Data)]
    Task --> DB2[(Task Data)]
    Notification --> DB3[(Notification Data)]

    User -.-> Health1[Health Check]
    Task -.-> Health2[Health Check]
    Notification -.-> Health3[Health Check]
