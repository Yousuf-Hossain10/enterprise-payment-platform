# Developer Instruction --- Enterprise Payment Platform (Production Simulation)

> **Mission Statement**
>
> Build a production-quality cloud-native payment platform from scratch.
> This project is intended to simulate how a modern fintech organization
> develops, deploys, monitors, secures, and operates microservices on
> Kubernetes.
>
> **The objective is not to finish quickly.**
>
> The objective is to build the platform exactly as a professional
> engineering team would.

------------------------------------------------------------------------

# IMPORTANT DEVELOPMENT RULES

Before writing a single line of code, read this entire document.

This project is expected to grow into a large enterprise codebase.

Therefore:

-   Never rush implementation.
-   Never generate placeholder code unless explicitly instructed.
-   Every decision must be production-oriented.
-   Every component must be modular.
-   Every feature must be documented.
-   Every commit should represent production-quality work.

The coding assistant should behave like a **Senior Software Architect +
Senior DevOps Engineer**, not merely a code generator.

------------------------------------------------------------------------

# Development Philosophy

This repository is intended to teach and demonstrate:

-   Enterprise software architecture
-   Clean code
-   Cloud Native Development
-   Kubernetes
-   DevOps
-   Platform Engineering
-   Observability
-   Security
-   CI/CD
-   Production deployment practices

Whenever multiple implementation choices exist, always choose the
approach that would be selected inside a mature engineering
organization.

------------------------------------------------------------------------

# Development Phases

The project **must** be developed incrementally.

Do not attempt to build everything simultaneously.

Each phase must be completed before the next phase begins.

Every phase must be fully functional.

Every phase must include documentation.

------------------------------------------------------------------------

# PHASE 1 --- Architecture & Planning

Before implementing anything, generate comprehensive architecture
documentation.

Create the following documents inside:

``` text
/docs
```

Generate:

-   Architecture.md
-   Technology-Decisions.md
-   Folder-Structure.md
-   Coding-Standards.md
-   Microservice-Responsibilities.md
-   API-Guidelines.md
-   Deployment-Strategy.md
-   Security-Model.md
-   Logging-Strategy.md
-   Observability-Strategy.md
-   Development-Roadmap.md

Nothing should be implemented before these documents exist.

## Architecture Diagrams

Generate Mermaid diagrams for:

-   System Context Diagram
-   Container Diagram
-   Component Diagram (per microservice)
-   Deployment Diagram
-   Sequence Diagrams (Login, Payment, Wallet Debit, Refund,
    Notification, Audit Logging, JWT Refresh)

------------------------------------------------------------------------

# PHASE 2 --- Repository Initialization

Create the repository structure as a professional monorepo.

------------------------------------------------------------------------

# PHASE 3 --- Local Infrastructure

Automate installation of:

-   Docker
-   Kind
-   NGINX Ingress
-   RabbitMQ
-   Redis
-   PostgreSQL
-   Metrics Server
-   Prometheus
-   Grafana
-   Loki
-   Namespaces
-   Storage Classes
-   Secrets

Everything should be provisioned through:

``` text
./scripts/bootstrap.ps1
```

or

``` text
./scripts/bootstrap.sh
```

------------------------------------------------------------------------

# PHASES 4--17

Implement sequentially:

1.  Shared backend foundation libraries.
2.  Identity Service.
3.  Wallet Service.
4.  Payment Service.
5.  Notification Service.
6.  Audit Service.
7.  Angular Frontend.
8.  Docker packaging.
9.  Kubernetes manifests.
10. Helm charts.
11. GitHub Actions enterprise CI/CD.
12. Observability stack.
13. Security hardening.
14. Comprehensive testing.
15. Complete documentation.

Each phase must be production-ready before proceeding.

------------------------------------------------------------------------

# Engineering Standards

Every Pull Request must satisfy:

-   Unit tests passing
-   Integration tests passing
-   Docker build succeeds
-   Kubernetes manifests validate
-   Helm lint passes
-   Documentation updated
-   No compiler warnings

------------------------------------------------------------------------

# Coding Standards

Use:

-   SOLID
-   Clean Architecture
-   Dependency Injection
-   Repository Pattern
-   Async/Await
-   Cancellation Tokens
-   Global Exception Middleware
-   Problem Details
-   Options Pattern
-   Strongly Typed Configuration

Avoid duplicated logic and magic strings.

------------------------------------------------------------------------

# Production Readiness Checklist

Before any feature is complete:

-   Documentation updated
-   Tests passing
-   Health endpoints implemented
-   Structured logging enabled
-   Metrics and traces exposed
-   Docker image builds
-   Helm deploys successfully
-   Kubernetes rollout succeeds
-   Smoke tests pass
-   No secrets committed
-   Environment-specific configuration
-   Failure scenarios handled

------------------------------------------------------------------------

# Final Goal

The repository should allow a developer to clone it, execute a single
bootstrap script, and obtain:

-   Local Kind Kubernetes cluster
-   Infrastructure services
-   Production-style .NET 8 microservices
-   Angular frontend
-   GitHub Actions CI/CD with self-hosted runner
-   Full observability
-   Complete architecture documentation and operational runbooks

## Agent Execution Rules

1.  Complete one phase at a time.
2.  Wait for review before beginning the next phase.
3.  Keep commits small and atomic.
4.  Explain architectural decisions.
5.  Build for long-term maintainability.

The final deliverable should be a production-inspired reference
implementation demonstrating modern software engineering, Kubernetes,
cloud-native architecture, and DevOps best practices.
