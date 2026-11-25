# M2 Security Configuration

## Security Roles

Three custom Dataverse security roles were created:

- Sales Representative
- Sales Manager
- Sales Administrator

All three roles were based on Basic User.

Their table privileges were configured according to SRS §6.1.

## Business Units

Three Business Units are configured:

- Root Business Unit
- Sales A
- Sales B

Sales A and Sales B are siblings under the Root Business Unit.

## Test Users

| User | Business Unit | Manager |
|---|---|---|
| Rep A1 | Sales A | Manager A |
| Rep A2 | Sales A | Manager A |
| Manager A | Sales A | — |
| Rep B1 | Sales B | Manager B |
| Manager B | Sales B | — |
| Sales Admin | Root | — |

All six users have Basic User and their corresponding custom sales role.

## Opportunity Business Process Flow

The generated `Opportunity Sales Process` table was found after activating the BPF.

Its privileges were configured for all three sales roles:

- Read: Organization
- Create, Write, Delete, Append, Append To, Assign, Share: None

## Hierarchy Security

Hierarchy Security remains disabled.

## Known Limitations

1. Task and Phone Call tables were not available in the security-role editor. Activity Pointer privileges were configured instead. Task/Phone Call-specific privileges remain unverified.
2. Business Unit data isolation and record-level visibility have not been independently tested.
3. The generated BPF table privileges have been configured, but runtime BPF access has not been independently verified.

## App Access

VNEXT CRM was shared with all six test users.

All six users successfully opened the app and its pages using the direct app URL.

## Status

M2 security configuration documented. Remaining limitations must be considered during integration testing.