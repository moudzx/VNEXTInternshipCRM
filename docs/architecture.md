# VNEXT Internship CRM — High-Level Architecture

## Overview

The CRM is built on Microsoft Power Platform, with Dataverse as its
central business data platform.

## Architecture Diagram

```mermaid
flowchart TD
    U[Sales Representatives and Managers]

    APP[Model-Driven Power App]
    DV[(Microsoft Dataverse)]
    API[C# Custom APIs]
    FLOW[Power Automate Flows]
    AGENT[Copilot Studio Agents]
    BI[Power BI Reports]

    U --> APP
    APP <--> DV
    APP --> API
    API <--> DV
    FLOW <--> DV
    AGENT --> API
    AGENT <--> DV
    BI --> DV

---

## Implementation Architecture

### 1. Plugin Layer

The backend business logic is implemented using C# plugins built on the Microsoft Dataverse SDK.

| Component | Responsibility |
|---|---|
| `QualifyLeadPlugin` | Handles Lead qualification logic (EXT10) |
| `TransitionOpportunityPlugin` | Enforces Opportunity lifecycle transitions (EXT11) |

Shared infrastructure is organized under the `Common` namespace.

### 2. Opportunity Transition API

The `vn_TransitionOpportunity` Custom API exposes controlled Opportunity transitions.

Supported commands:

- **Advance:** Moves an Opportunity to the next permitted sales stage.
- **Won:** Completes an Opportunity as won.
- **Lost:** Completes an Opportunity as lost.

The API delegates business validation and execution to `TransitionOpportunityPlugin`.

### 3. Business Process Flow

The `vn_opportunitysalesprocess` BPF represents the Opportunity sales lifecycle.

Its stages are:

1. Qualify
2. Develop
3. Propose
4. Close

The transition logic coordinates Opportunity updates with the corresponding BPF operations.

### 4. Dataverse Interaction

Plugins interact with Dataverse through the Organization Service.

Key responsibilities include:

- Retrieving and updating business records.
- Validating Opportunity ownership and user permissions.
- Checking required information before stage transitions.
- Applying optimistic concurrency using row versions.
- Maintaining transaction consistency.

### 5. Security Architecture

The implementation includes:

- Role-based authorization.
- Business Unit and ownership validation.
- Controlled access to Opportunity transitions.
- Server-side validation of business rules.
- Privileged service execution for internal BPF operations.

### 6. Execution Flow

```mermaid
flowchart TD
    U[User] --> APP[Model-Driven App]
    APP --> API[vn_TransitionOpportunity]
    API --> P[TransitionOpportunityPlugin]
    P --> AUTH[Authorization Checks]
    AUTH --> VALID[Business Rule Validation]
    VALID --> UPDATE[Update Opportunity]
    UPDATE --> BPF[Update BPF]
    BPF --> RESPONSE[Return API Response]
```

### 7. Implementation Status

| Component | Status |
|---|---|
| Dataverse data model | Implemented |
| Model-driven application | Project component |
| EXT10 Lead qualification plugin | Registered |
| EXT11 Opportunity transition plugin | Registered |
| Opportunity Custom API | Registered |
| Opportunity BPF | Included in solution |
| Power Automate integration | Planned / verify |
| Copilot Studio integration | Planned / verify |
| Power BI reporting | Planned / verify |

*Note: Deployment, execution identity, and functional validation remain subject to final verification.*