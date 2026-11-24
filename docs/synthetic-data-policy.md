# Synthetic Data Policy

## Purpose

All development, testing, and demonstration records must use
synthetic data unless VNEXT explicitly authorizes another approach.

## Data Rules

- Never use real customer or prospect information.
- Never copy production records into the development environment.
- Use fictional names, companies, email addresses, and phone numbers.
- Use reserved example domains such as example.com.
- Do not include real credentials, access tokens, or secrets.
- Keep synthetic records clearly distinguishable from real records.
- Document seed-data provenance and generation conventions.

## Role Identities

The CRM design includes these business identities:

- Sales Representative
- Sales Manager
- Sales Administrator
- Worker connection (automation identity)

These are logical roles, not confirmed real users.

## Business Unit Conventions

The implementation must support separate Business Units
so that same-BU and cross-BU access can be tested.

Actual users, role assignments, and Business Unit arrangements
must be agreed with VNEXT before security testing.

## Approval Safety

- A Manager must be a genuine authorized human user.
- A Representative cannot approve their own deal.
- An automation identity cannot impersonate a Manager.
- Test approval scenarios must not be represented as genuine
  approval decisions.

## Outstanding Confirmation

- VNEXT-approved test identities
- Business Unit structure
- Authorized automation identity
- Approved synthetic-data seed conventions
