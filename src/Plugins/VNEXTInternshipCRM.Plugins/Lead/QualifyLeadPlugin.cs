
using System;
using Microsoft.Xrm.Sdk;
using VNEXTInternshipCRM.Plugins.Common.Context;
using VNEXTInternshipCRM.Plugins.Common.Exceptions;
using VNEXTInternshipCRM.Plugins.Common.Services;
using VNEXTInternshipCRM.Plugins.Common.Validation;
using VNEXTInternshipCRM.Plugins.Infrastructure;

namespace VNEXTInternshipCRM.Plugins.Lead
{
    public sealed class QualifyLeadPlugin : CrmPluginBase
    {
        private const int LeadQualified = 100000002;
        private const int OpportunityQualifyStage = 100000000;
        private const int AccountProspect = 100000000;

        public QualifyLeadPlugin(
            string unsecureConfiguration,
            string secureConfiguration)
            : base(typeof(QualifyLeadPlugin))
        {
        }

        protected override void Execute(PluginExecutionContext context)
        {
            context.RequireTransaction();

            var service = new DataverseService(context.Service);

            EntityReference target =
                PluginValidator.RequireInput<EntityReference>(
                    context.Context,
                    "Target");

            if (target.LogicalName != "vn_lead" ||
                target.Id == Guid.Empty)
            {
                throw CrmPluginException.Create(
                    "CRM001",
                    "Target must reference a valid Lead.");
            }

            string expectedRowVersion =
                PluginValidator.RequireInput<string>(
                    context.Context,
                    "ExpectedRowVersion");

            string opportunityName =
                PluginValidator.RequireInput<string>(
                    context.Context,
                    "OpportunityName");

            DateTime expectedCloseDate =
                PluginValidator.RequireInput<DateTime>(
                    context.Context,
                    "ExpectedCloseDate");

            PluginValidator.RequireNonEmptyString(
                expectedRowVersion,
                "ExpectedRowVersion");

            PluginValidator.RequireNonEmptyString(
                opportunityName,
                "OpportunityName");

            opportunityName = opportunityName.Trim();

            if (opportunityName.Length > 200)
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "Opportunity Name cannot exceed 200 characters.");
            }

            if (expectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "Expected Close Date cannot be earlier than the current UTC date.");
            }

            Guid? existingAccountId =
                ReadOptionalGuid("ExistingAccountId", context);

            Guid? existingContactId =
                ReadOptionalGuid("ExistingContactId", context);

            if (existingContactId.HasValue &&
                !existingAccountId.HasValue)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "An Existing Contact requires an Existing Account.");
            }

            Entity lead = service.Retrieve(
                "vn_lead",
                target.Id,
                "vn_name",
                "vn_firstname",
                "vn_lastname",
                "vn_companyname",
                "vn_email",
                "vn_phone",
                "vn_need",
                "vn_nextstep",
                "vn_nextfollowupdate",
                "vn_budget",
                "ownerid",
                "statecode",
                "statuscode",
                "vn_account",
                "vn_contact",
                "vn_opportunity",
                "vn_qualifiedon");

            var authorization =
                new AuthorizationService(context.Service);

            authorization.ValidateLeadQualification(
                context.Context.InitiatingUserId,
                lead);

            if (IsAlreadyQualified(lead))
            {
                ReturnExistingQualification(lead, context);
                return;
            }

            if (!string.Equals(
                lead.RowVersion,
                expectedRowVersion,
                StringComparison.Ordinal))
            {
                throw CrmPluginException.Create(
                    "CRM021",
                    "The Lead has changed. Refresh it and retry qualification.");
            }

            LeadQualificationValidator.Validate(lead);

            EntityReference owner =
                lead.GetAttributeValue<EntityReference>("ownerid");

            if (owner == null)
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "The Lead must have an owner before qualification.");
            }

            Entity account;
            bool createdAccount = false;

            if (existingAccountId.HasValue)
            {
                account = service.Retrieve(
                    "account",
                    existingAccountId.Value,
                    "name",
                    "statecode",
                    "ownerid",
                    "primarycontactid");

                EnsureActive(account, "Account");
            }
            else
            {
                account = CreateAccount(lead, owner);
                account.Id = service.Create(account);
                createdAccount = true;
            }

            Entity contact;
            bool createdContact = false;

            if (existingContactId.HasValue)
            {
                contact = service.Retrieve(
                    "contact",
                    existingContactId.Value,
                    "firstname",
                    "lastname",
                    "parentcustomerid",
                    "statecode",
                    "ownerid");

                EnsureActive(contact, "Contact");

                EntityReference parent =
                    contact.GetAttributeValue<EntityReference>(
                        "parentcustomerid");

                if (parent == null ||
                    parent.LogicalName != "account" ||
                    parent.Id != account.Id)
                {
                    throw CrmPluginException.Create(
                        "CRM004",
                        "The selected Contact does not belong to the selected Account.");
                }
            }
            else
            {
                contact = CreateContact(
                    lead,
                    account.ToEntityReference(),
                    owner);

                contact.Id = service.Create(contact);
                createdContact = true;
            }

            if (createdAccount)
            {
                var accountUpdate = new Entity("account", account.Id);
                accountUpdate["primarycontactid"] =
                    contact.ToEntityReference();

                service.Update(accountUpdate);
            }

            EntityReference baseCurrency =
                service.RetrieveBaseCurrency();

            Entity opportunity = CreateOpportunity(
                opportunityName,
                expectedCloseDate,
                lead,
                account.ToEntityReference(),
                contact.ToEntityReference(),
                owner,
                baseCurrency);

            opportunity.Id = service.Create(opportunity);

            // Create the BPF instance using the configured plug-in identity.
            // This remains inside the same synchronous Dataverse transaction.
            var bpfService =
                new BusinessProcessFlowService(context.PluginUserService);

            Guid bpfId =
                bpfService.CreateForOpportunity(opportunity.Id);

            DateTime qualifiedOn = DateTime.UtcNow;

            var leadUpdate = new Entity("vn_lead", lead.Id)
            {
                RowVersion = lead.RowVersion
            };

            leadUpdate["statecode"] = new OptionSetValue(1);
            leadUpdate["statuscode"] = new OptionSetValue(LeadQualified);
            leadUpdate["vn_account"] = account.ToEntityReference();
            leadUpdate["vn_contact"] = contact.ToEntityReference();
            leadUpdate["vn_opportunity"] = opportunity.ToEntityReference();
            leadUpdate["vn_qualifiedon"] = qualifiedOn;

            service.UpdateIfRowVersionMatches(leadUpdate);

            context.Context.OutputParameters["AccountId"] = account.Id;
            context.Context.OutputParameters["ContactId"] = contact.Id;
            context.Context.OutputParameters["OpportunityId"] = opportunity.Id;
            context.Context.OutputParameters["WasAlreadyQualified"] = false;
            context.Context.OutputParameters["CorrelationId"] =
                context.Context.CorrelationId.ToString();

            context.Tracing.Trace(
                "EXT10 completed. Lead: " + lead.Id +
                ", Opportunity: " + opportunity.Id +
                ", BPF: " + bpfId +
                ", Account created: " + createdAccount +
                ", Contact created: " + createdContact);
        }

        private static Guid? ReadOptionalGuid(
            string parameterName,
            PluginExecutionContext context)
        {
            if (!context.Context.InputParameters.Contains(parameterName))
                return null;

            object value = context.Context.InputParameters[parameterName];

            if (!(value is Guid))
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    parameterName + " must be a GUID.");
            }

            Guid id = (Guid)value;

            if (id == Guid.Empty)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    parameterName + " cannot be empty.");
            }

            return id;
        }

        private static bool IsAlreadyQualified(Entity lead)
        {
            int state =
                lead.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;

            int status =
                lead.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1;

            return state == 1 &&
                   status == LeadQualified &&
                   lead.GetAttributeValue<EntityReference>("vn_account") != null &&
                   lead.GetAttributeValue<EntityReference>("vn_contact") != null &&
                   lead.GetAttributeValue<EntityReference>("vn_opportunity") != null;
        }

        private static void ReturnExistingQualification(
            Entity lead,
            PluginExecutionContext context)
        {
            context.Context.OutputParameters["AccountId"] =
                lead.GetAttributeValue<EntityReference>("vn_account").Id;

            context.Context.OutputParameters["ContactId"] =
                lead.GetAttributeValue<EntityReference>("vn_contact").Id;

            context.Context.OutputParameters["OpportunityId"] =
                lead.GetAttributeValue<EntityReference>("vn_opportunity").Id;

            context.Context.OutputParameters["WasAlreadyQualified"] = true;
            context.Context.OutputParameters["CorrelationId"] =
                context.Context.CorrelationId.ToString();
        }

        private static void EnsureActive(Entity entity, string label)
        {
            int state =
                entity.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;

            if (state != 0)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The selected " + label + " must be active.");
            }
        }

        private static Entity CreateAccount(Entity lead, EntityReference owner)
        {
            var account = new Entity("account");

            account["name"] =
                lead.GetAttributeValue<string>("vn_companyname").Trim();

            account["vn_accounttype"] =
                new OptionSetValue(AccountProspect);

            CopyIfPresent(lead, account, "vn_phone", "telephone1");
            account["ownerid"] = owner;

            return account;
        }

        private static Entity CreateContact(
            Entity lead,
            EntityReference account,
            EntityReference owner)
        {
            var contact = new Entity("contact");

            string firstName =
                lead.GetAttributeValue<string>("vn_firstname");

            string email =
                lead.GetAttributeValue<string>("vn_email");

            string phone =
                lead.GetAttributeValue<string>("vn_phone");

            if (!string.IsNullOrWhiteSpace(firstName))
                contact["firstname"] = firstName.Trim();

            contact["lastname"] =
                lead.GetAttributeValue<string>("vn_lastname").Trim();

            contact["parentcustomerid"] = account;

            if (!string.IsNullOrWhiteSpace(email))
            {
                contact["emailaddress1"] = email.Trim();
                contact["vn_preferredmethod"] = new OptionSetValue(100000000);
            }
            else
            {
                contact["mobilephone"] = phone.Trim();
                contact["vn_preferredmethod"] = new OptionSetValue(100000001);
            }

            contact["ownerid"] = owner;

            return contact;
        }

        private static Entity CreateOpportunity(
            string name,
            DateTime expectedCloseDate,
            Entity lead,
            EntityReference account,
            EntityReference contact,
            EntityReference owner,
            EntityReference baseCurrency)
        {
            var opportunity = new Entity("vn_opportunity");

            DateTime now = DateTime.UtcNow;

            opportunity["vn_name"] = name;
            opportunity["vn_account"] = account;
            opportunity["vn_primarycontact"] = contact;
            opportunity["vn_originatinglead"] = lead.ToEntityReference();

            opportunity["transactioncurrencyid"] = baseCurrency;

            opportunity["vn_expectedclosedate"] = expectedCloseDate.Date;
            opportunity["vn_nextstep"] =
                lead.GetAttributeValue<string>("vn_nextstep").Trim();

            opportunity["vn_nextfollowupdate"] =
                lead.GetAttributeValue<DateTime>("vn_nextfollowupdate").Date;

            opportunity["vn_stage"] =
                new OptionSetValue(OpportunityQualifyStage);

            opportunity["vn_probability"] = 10;
            opportunity["vn_total"] = new Money(0m);
            opportunity["vn_maximumdiscount"] = 0m;
            opportunity["vn_commandsequence"] = 0;
            opportunity["vn_currentapproval"] = null;
            opportunity["vn_lastprogresson"] = now;
            opportunity["vn_stageenteredon"] = now;

            opportunity["ownerid"] = owner;

            return opportunity;
        }

        private static void CopyIfPresent(
            Entity source,
            Entity target,
            string sourceAttribute,
            string targetAttribute)
        {
            string value = source.GetAttributeValue<string>(sourceAttribute);

            if (!string.IsNullOrWhiteSpace(value))
                target[targetAttribute] = value.Trim();
        }
    }
}
