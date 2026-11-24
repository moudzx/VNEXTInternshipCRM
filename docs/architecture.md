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
