
using System;
using System.Net.Mail;
using Microsoft.Xrm.Sdk;
using VNEXTInternshipCRM.Plugins.Common.Exceptions;

namespace VNEXTInternshipCRM.Plugins.Common.Validation
{
    public static class LeadQualificationValidator
    {
        public static void Validate(Entity lead)
        {
            if (lead == null || lead.LogicalName != "vn_lead")
            {
                throw CrmPluginException.Create(
                    "CRM001",
                    "The requested Lead could not be found.");
            }

            int state = lead.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1;
            int status = lead.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1;

            if (state != 0)
            {
                throw CrmPluginException.Create(
                    "CRM005",
                    "Only an active Lead can be qualified.");
            }

            if (status != 100000001)
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "The Lead must be in the Contacted status before qualification.");
            }

            RequireText(lead, "vn_companyname", "Company Name");
            RequireText(lead, "vn_lastname", "Last Name");
            RequireText(lead, "vn_need", "Need");
            RequireText(lead, "vn_nextstep", "Next Step");

            if (!lead.Contains("vn_nextfollowupdate"))
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "Next Follow-up Date is required.");
            }

            string email = lead.GetAttributeValue<string>("vn_email");
            string phone = lead.GetAttributeValue<string>("vn_phone");

            if (string.IsNullOrWhiteSpace(email) &&
                string.IsNullOrWhiteSpace(phone))
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "At least one contact method is required.");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                try
                {
                    var address = new MailAddress(email);

                    if (!string.Equals(
                        address.Address,
                        email.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new FormatException();
                    }
                }
                catch (FormatException)
                {
                    throw CrmPluginException.Create(
                        "CRM002",
                        "The Lead email address is invalid.");
                }
            }

            Money budget = lead.GetAttributeValue<Money>("vn_budget");

            if (budget != null && budget.Value < 0)
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    "Lead Budget cannot be negative.");
            }
        }

        private static void RequireText(
            Entity entity,
            string attribute,
            string label)
        {
            if (string.IsNullOrWhiteSpace(
                entity.GetAttributeValue<string>(attribute)))
            {
                throw CrmPluginException.Create(
                    "CRM002",
                    label + " is required for qualification.");
            }
        }
    }
}
