# M1 — Dataverse Foundation

## 1. Objective

Establish the Dataverse data foundation for the VNEXT Internship CRM:
custom tables, native table extensions, columns, relationships, alternate
keys, ownership, and auditing.

Source of requirements: VNEXT Internship 2026 CRM BRD/SRS, Version 1.4.2.

## 2. Environment

| Property | Value |
|---|---|
| Environment | Mohamad Mostafa's Environment |
| Type | Developer |
| Region | Europe |
| Base Currency | Lebanese Pound |
| Solution | VNEXTInternshipCRM |
| Publisher Prefix | vn |

No Dynamics 365 Sales installation is assumed or required.

## 3. Custom Tables

Exactly five custom tables were created.

| Table | Logical Name | Ownership |
|---|---|---|
| Lead | vn_lead | User or team |
| Opportunity | vn_opportunity | User or team |
| Product | vn_product | Organization |
| Opportunity Line | vn_opportunityline | User or team |
| Approval Request | vn_approvalrequest | User or team |

Each custom table has a primary name column using the `vn_name`
logical name, configured as Text with a maximum length of 200.

## 4. Native Table Extensions

The following native tables were added to the solution and configured
with the required project-specific columns.

### Account

- Industry (`vn_industry`): Choice.
- Account Type (`vn_accounttype`): Choice, default Prospect.
- Existing native Account columns reused, including Account Name,
  Account Number, Main Phone, Email, Website, City, Country,
  Primary Contact and Street.

### Contact

- Preferred Method (`vn_preferredmethod`): Choice.
- Do Not Contact (`vn_donotcontact`): Yes/No, default No.
- Existing native Contact columns reused, including First Name,
  Last Name, Full Name, Parent Customer, Email, Mobile Phone and Job Title.

### Task

- Automation Key (`vn_automationkey`): Text, maximum 200.
- Alternate key `vn_TaskAutomationKey` created and Active.

### Phone Call

Native Subject, Owner, Description, Direction, From, To, Scheduled Start,
Scheduled End, Regarding, Status and Status Reason columns are reused.

## 5. Alternate Keys

| Table | Key | Column | Observed Status |
|---|---|---|---|
| Product | vn_ProductSKUKey | vn_sku | Active |
| Task | vn_TaskAutomationKey | vn_automationkey | Active |
| Opportunity | vn_OpportunityOriginatingLeadKey | vn_originatinglead | Active |

The Opportunity key enforces uniqueness of the originating Lead lookup.

Platform limitation: Dataverse alternate-key uniqueness is not conditional
on a record being Active. The Product SKU key therefore does not by itself
implement the SRS requirement for Active-only SKU uniqueness.

## 6. Relationships and Delete Behavior

The following relationships were reviewed and configured with
Referential behavior and Restrict deletion.

| Relationship | Lookup |
|---|---|
| Lead to resulting Opportunity | vn_originatinglead |
| Account to Opportunity | vn_account |
| Contact to Opportunity | vn_primarycontact |
| Opportunity to Opportunity Line | vn_opportunity |
| Product to Opportunity Line | vn_product |
| Opportunity to Approval Request | vn_opportunity |
| Lead to Duplicate Lead | vn_duplicateof |
| Lead to Account | vn_account |
| Lead to Contact | vn_contact |

Other cascade settings were left at None where reviewed.

Native Contact `parentcustomerid` and Account `primarycontactid`
relationships were identified but were not exposed in the solution
relationship views. Their cascade metadata remains pending verification.

## 7. Activity Configuration

- Lead and Opportunity were enabled as options when creating activities.
- Account and Contact were also enabled as activity targets.
- Task Regarding relationships to Lead and Opportunity were confirmed.
- Phone Call Regarding relationships to Lead and Opportunity were confirmed.
- Phone Call From, To and Direction columns were confirmed.

Actual activity-party target compatibility and full native activity
metadata remain pending verification.

## 8. Auditing

Environment auditing was enabled.

Table auditing was enabled for:

- Lead
- Opportunity
- Product
- Opportunity Line
- Approval Request
- Account
- Contact
- Task
- Phone Call

Column auditing was enabled for the selected business-critical fields,
including lifecycle outcomes, relationships, ownership, pricing,
approval results, customer contactability and activity status.

## 9. Known Deviations and Verification Items

### Status Reason values

The Power Apps designer generated Status Reason values that differ
from the numeric values specified in the SRS.

Observed Lead values:

- New: 1
- Contacted: 127440001
- Qualified: 2
- Disqualified: 127440002

Observed Opportunity values:

- Open: 1
- Won: 2
- Lost: 127440001

These are Status Reason (`statuscode`) values, not State (`statecode`)
values. The designer did not allow editing the generated values.
A supported metadata-level resolution remains to be investigated.

### Alternate-key limitation

The Product SKU alternate key does not provide Active-only uniqueness.
Additional server-side enforcement is required.

### Native metadata

The following remain pending deeper verification:

- Native Account and Contact relationship cascade metadata.
- Native Task and Phone Call state and Status Reason metadata.
- Phone Call ActivityParty target compatibility.
- Full native Regarding metadata and supported targets.

These items must not be treated as verified merely because the
corresponding columns or relationships are visible.

## 10. M1 Completion Status

M1 is formally accepted. The custom schema, native extensions, lookup
relationships, alternate keys, ownership configuration and auditing setup
have been established.

The following documented limitations remain as follow-up items:

- Lead and Opportunity Status Reason metadata deviations.
- Native relationship and activity metadata verification.
- Active-only Product SKU uniqueness enforcement.

Business operations, server-side validation, Custom APIs, approval
processing and integration tests belong to subsequent milestones.
