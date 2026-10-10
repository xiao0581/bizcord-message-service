This document represents the initial analysis and high-level architecture design from Week 36. The implementation developed in subsequent weeks focuses on the Messaging Microservice.

# Bizcord – Analysis and Design

## 1. Project Overview

Bizcord is a Discord-like communication platform designed using a microservice architecture.

The system is divided into independent services, each responsible for a specific business domain.

For this assignment, I am responsible for developing the **Messaging Microservice**.

## 2. Domains and Microservice Responsibilities

The system consists of the following planned services:

| Microservice | Responsibility |
|---|---|
| Messaging Service | Sending, storing, retrieving, editing and deleting messages |
| Channel Service | Managing channels, memberships and permissions |
| User Profile Service | Managing user profiles and settings |
| Authentication Service | Managing authentication and authorization |
| Voice Service | Managing voice communication |
| Video Service | Managing video communication |
| Engagement Service | Managing notifications, reactions and other engagement features |

Each service has its own responsibilities and boundaries.

## 3. Service Boundaries

The Messaging Service is responsible for managing the lifecycle of chat messages.

It does not manage user authentication, channel memberships or notifications. These responsibilities belong to other microservices.

This separation reduces coupling between services and allows them to be developed and deployed independently.

## 4. Data Ownership

Each microservice owns its domain data.

- **Messaging Service:** Message ID, sender ID, channel ID, message content and creation timestamp.
- **Channel Service:** Channels, memberships and channel permissions.
- **User Profile Service:** User profiles and settings.
- **Authentication Service:** Authentication credentials and related security data.
- **Engagement Service:** Notifications and reactions.
- **Voice Service:** Voice session information.
- **Video Service:** Video session information.

Other services should access this information through defined interfaces rather than directly accessing another service's internal data storage.

## 5. Inter-service Communication

The system is designed to support two types of communication:

### Synchronous Communication

The Messaging Service will expose REST APIs for creating, retrieving, updating and deleting messages.

Clients will use these APIs to interact directly with the service.

### Asynchronous Communication

The Messaging Service may publish events when messages are created.

Other microservices can react to these events without directly depending on the Messaging Service.

For example, the Engagement Service could receive a message event and use it to trigger a notification.

This approach helps reduce coupling between microservices.

## 6. Messaging Service Design

The Messaging Service is designed around the following responsibilities:

- **API Layer:** Receives requests from clients and returns responses.
- **Business Logic:** Handles message creation, retrieval, editing and deletion.
- **Data Management:** Stores and retrieves message information.
- **Service Communication:** Provides interfaces for communication with other microservices.

The service should be independently deployable and should not directly access the internal data storage of other services.

The implementation details will be developed in the following weeks.

## 7. Architecture Diagram

The architecture diagram illustrates the planned Bizcord microservices, their responsibilities, service boundaries and interactions.

![Bizcord Architecture](Bizcord%20Microservices%20Architecture.png)

The diagram represents the overall system design. The implementation in this assignment focuses on the Messaging Microservice.