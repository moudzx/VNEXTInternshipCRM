# VNEXTInternshipCRM — Deployment Guide

## 1. Overview

This document describes the deployment of the VNEXTInternshipCRM solution to a Microsoft Dataverse environment.

The project includes custom Dataverse plugins, a Custom API, and an Opportunity Business Process Flow.

## 2. Solution Information

| Property | Value |
|---|---|
| Project | VNEXTInternshipCRM |
| Platform | Microsoft Power Platform |
| Backend | Microsoft Dataverse |
| Plugin Framework | Microsoft.Xrm.Sdk |
| Plugin Target | .NET Framework 4.8 |
| Solution Type | Unmanaged (Development) |

## 3. Solution Components

### Custom Plugins

| Plugin | Responsibility |
|---|---|
| QualifyLeadPlugin | Handles Lead qualification (EXT10) |
| TransitionOpportunityPlugin | Controls Opportunity stage transitions (EXT11) |

### Custom API

| Property | Value |
|---|---|
| Unique Name | `vn_TransitionOpportunity` |
| Display Name | Transition Opportunity |
| Binding Type | Entity |
| Bound Entity | opportunity |
| Operation | Synchronous |
| Function | No |

The API supports the following commands:

- `Advance`
- `Won`
- `Lost`

Request parameters:

- `Command`
- `ExpectedRowVersion`
- `TargetStage`
- `Comments`
- `ActualCloseDate`
- `LossReason`

Response properties:

- `Stage`
- `StatusReason`
- `CommandSequence`
- `CorrelationId`

### Business Process Flow

| Property | Value |
|---|---|
| Logical Name | `vn_opportunitysalesprocess` |
| Primary Entity | Opportunity |

The BPF represents the Opportunity sales process and is managed by the transition plugin.

## 4. Build Requirements

- Windows development environment
- Visual Studio
- .NET Framework 4.8 Developer Pack
- Microsoft Power Platform Dataverse environment