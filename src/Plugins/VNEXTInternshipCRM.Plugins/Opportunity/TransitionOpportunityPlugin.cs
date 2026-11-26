using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using VNEXTInternshipCRM.Plugins.Common.Context;
using VNEXTInternshipCRM.Plugins.Common.Exceptions;
using VNEXTInternshipCRM.Plugins.Common.Services;
using VNEXTInternshipCRM.Plugins.Common.Validation;
using VNEXTInternshipCRM.Plugins.Infrastructure;

namespace VNEXTInternshipCRM.Plugins.Opportunity
{
    /// <summary>
    /// Main operation for the Opportunity-bound Custom API vn_TransitionOpportunity.
    /// Register synchronously in the main operation pipeline and require a transaction.
    /// </summary>
    public sealed class TransitionOpportunityPlugin : CrmPluginBase
    {
        private const string OpportunityTable = "vn_opportunity";
        private const int OpenState = 0;
        private const int InactiveState = 1;
        private const int OpenStatus = 100000000;
        private const int WonStatus = 100000001;
        private const int LostStatus = 100000002;
        private const int Qualify = 100000000;
        private const int Develop = 100000001;
        private const int Propose = 100000002;
        private const int Close = 100000003;
        private const int WonProbability = 100;
        private const int LostProbability = 0;
        private const decimal ApprovalTotalThreshold = 50000m;
        private const decimal ApprovalDiscountThreshold = 10m;

        private static readonly int[] ValidLossReasons =
        {
            100000000, 100000001, 100000002,
            100000003, 100000004, 100000005
        };

        public TransitionOpportunityPlugin(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(TransitionOpportunityPlugin)) { }

        protected override void Execute(PluginExecutionContext context)
        {
            context.RequireTransaction();
            var execution = context.Context;
            var target = PluginValidator.RequireInput<EntityReference>(execution, "Target");
            if (target.LogicalName != OpportunityTable || target.Id == Guid.Empty)
                throw CrmPluginException.Create("CRM001", "Target must reference a valid Opportunity.");

            string command = PluginValidator.RequireInput<string>(execution, "Command");
            string expectedVersion = NormalizeRowVersion(
                PluginValidator.RequireInput<string>(execution, "ExpectedRowVersion"));
            if (string.IsNullOrWhiteSpace(expectedVersion))
                throw CrmPluginException.Create("CRM021", "ExpectedRowVersion is required.");

            command = command.Trim();
            if (command != "Advance" && command != "Won" && command != "Lost")
                throw CrmPluginException.Create("CRM002", "Command must be Advance, Won or Lost.");

            // This read is deliberately performed with the initiating caller's service.
            // It both retrieves the current RowVersion and enforces record-level Read access.
            var callerService = new DataverseService(context.Service);
            Entity opportunity = callerService.Retrieve(
                OpportunityTable, target.Id,
                "vn_name", "ownerid", "statecode", "statuscode", "vn_stage",
                "vn_probability", "vn_stageenteredon", "vn_actualclosedate",
                "vn_closecomments", "vn_closedamount", "vn_lossreason",
                "vn_total", "vn_maximumdiscount", "vn_commandsequence",
                "vn_currentapproval", "vn_decisionmaker", "vn_need",
                "vn_account", "vn_primarycontact", "vn_expectedclosedate",
                "vn_nextstep", "vn_proposalsummary", "vn_lastprogresson",
                "createdon");

            ValidateCaller(context, opportunity);
            if ((opportunity.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1) != OpenState ||
                (opportunity.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1) != OpenStatus)
                throw CrmPluginException.Create("CRM005", "Only an Active/Open Opportunity can be transitioned.");

            if (!string.Equals(opportunity.RowVersion, expectedVersion, StringComparison.Ordinal))
                throw CrmPluginException.Create("CRM021", "The Opportunity changed. Refresh it and retry.");

            int currentStage = opportunity.GetAttributeValue<OptionSetValue>("vn_stage")?.Value ?? -1;
            if (currentStage < Qualify || currentStage > Close)
                throw CrmPluginException.Create("CRM002", "The saved Opportunity stage is invalid.");

            Entity update = new Entity(OpportunityTable, opportunity.Id) { RowVersion = opportunity.RowVersion };
            int sequence = opportunity.GetAttributeValue<int?>("vn_commandsequence") ?? 0;
            update["vn_commandsequence"] = checked(sequence + 1);
            DateTime now = DateTime.UtcNow;

            int responseStage;
            int responseStatus;
            if (command == "Advance")
            {
                int targetStage = PluginValidator.RequireInput<int>(execution, "TargetStage");
                if (targetStage != currentStage + 1 || targetStage > Close)
                    throw CrmPluginException.Create("CRM002", "Advance must move exactly one adjacent stage forward.");
                ValidateReadiness(context.Service, opportunity, targetStage);
                update["vn_stage"] = new OptionSetValue(targetStage);
                update["vn_probability"] = targetStage == Develop ? 30 : targetStage == Propose ? 60 : 90;
                update["vn_stageenteredon"] = now;
                update["vn_lastprogresson"] = now;
                responseStage = targetStage;
                responseStatus = OpenStatus;
            }
            else
            {
                string comments = ReadOptionalString(execution, "Comments");
                DateTime actualCloseDate = PluginValidator.RequireInput<DateTime>(execution, "ActualCloseDate").Date;
                ValidateCloseDate(opportunity, actualCloseDate);
                if (string.IsNullOrWhiteSpace(comments))
                    throw CrmPluginException.Create("CRM002", "Close comments are required for Won and Lost.");
                comments = comments.Trim();
                if (comments.Length > 2000)
                    throw CrmPluginException.Create("CRM002", "Close comments cannot exceed 2,000 characters.");

                update["vn_actualclosedate"] = actualCloseDate;
                update["vn_closecomments"] = comments;
                update["vn_lastprogresson"] = now;
                update["statecode"] = new OptionSetValue(InactiveState);

                if (command == "Won")
                {
                    if (currentStage != Qualify && currentStage != Close)
                        throw CrmPluginException.Create("CRM002", "Won is allowed only from Qualify or Close.");
                    decimal total = GetMoney(opportunity, "vn_total");
                    decimal maxDiscount = GetDecimal(opportunity, "vn_maximumdiscount");
                    if (total <= 0m || !HasPositiveLine(context.Service, opportunity.Id))
                        throw CrmPluginException.Create("CRM002", "Won requires at least one positive Line and a positive Total.");
                    if (RequiresApproval(total, maxDiscount))
                        ValidateCurrentApproval(context.Service, opportunity, total, maxDiscount);
                    update["statuscode"] = new OptionSetValue(WonStatus);
                    update["vn_probability"] = WonProbability;
                    update["vn_closedamount"] = new Money(total);
                    responseStage = currentStage;
                    responseStatus = WonStatus;
                }
                else
                {
                    int reason = PluginValidator.RequireInput<int>(execution, "LossReason");
                    if (!ValidLossReasons.Contains(reason))
                        throw CrmPluginException.Create("CRM002", "LossReason must be one of the six approved values.");
                    update["statuscode"] = new OptionSetValue(LostStatus);
                    update["vn_lossreason"] = new OptionSetValue(reason);
                    update["vn_probability"] = LostProbability;
                    update["vn_closedamount"] = new Money(0m);
                    responseStage = currentStage;
                    responseStatus = LostStatus;
                }
            }

            // Conditional parent write is the first parent-dependent mutation and is the
            // serialization point for all data changed by this command.
            callerService.UpdateIfRowVersionMatches(update);

            // BPF mirroring uses the configured plug-in execution identity, never SYSTEM.
            // The initiating user's Opportunity access was checked above.
            var bpf = new BusinessProcessFlowService(context.PluginUserService);
            if (command == "Advance")
                bpf.Advance(target.Id, responseStage);
            else if (command == "Won")
                bpf.Finish(target.Id);
            else
                bpf.Abort(target.Id);

            execution.OutputParameters["Stage"] = responseStage;
            execution.OutputParameters["StatusReason"] = responseStatus;
            execution.OutputParameters["CommandSequence"] = sequence + 1;
            execution.OutputParameters["CorrelationId"] = context.CorrelationId.ToString("D");
        }

        private static void ValidateCaller(PluginExecutionContext context, Entity opportunity)
        {
            EntityReference ownerRef = opportunity.GetAttributeValue<EntityReference>("ownerid");
            if (ownerRef == null || ownerRef.LogicalName != "systemuser")
                throw CrmPluginException.Create("CRM003", "The Opportunity must be owned by an enabled sales user.");

            Entity caller = context.Service.Retrieve("systemuser", context.InitiatingUserId,
                new ColumnSet("businessunitid", "isdisabled"));
            Entity owner = context.Service.Retrieve("systemuser", ownerRef.Id,
                new ColumnSet("businessunitid", "isdisabled"));
            if (caller.GetAttributeValue<bool>("isdisabled") || owner.GetAttributeValue<bool>("isdisabled"))
                throw CrmPluginException.Create("CRM003", "Disabled users cannot transition Opportunities.");

            var auth = new AuthorizationService(context.Service);
            bool rep = auth.HasRole(context.InitiatingUserId, "Sales Representative");
            bool manager = auth.HasRole(context.InitiatingUserId, "Sales Manager");
            if (!rep && !manager)
                throw CrmPluginException.Create("CRM003", "Only Sales Representatives and Sales Managers can transition Opportunities.");

            Guid callerBu = caller.GetAttributeValue<EntityReference>("businessunitid")?.Id ?? Guid.Empty;
            Guid ownerBu = owner.GetAttributeValue<EntityReference>("businessunitid")?.Id ?? Guid.Empty;
            if (callerBu == Guid.Empty || callerBu != ownerBu)
                throw CrmPluginException.Create("CRM003", "The caller and Opportunity owner must belong to the same Business Unit.");
            if (rep && ownerRef.Id != context.InitiatingUserId)
                throw CrmPluginException.Create("CRM003", "A Sales Representative can transition only their own Opportunity.");
        }

        private static void ValidateReadiness(IOrganizationService service, Entity opportunity, int targetStage)
        {
            RequireText(opportunity, "vn_need", "Need");
            RequireText(opportunity, "vn_decisionmaker", "Decision-Maker");
            RequireText(opportunity, "vn_nextstep", "Next Step");

            EntityReference contactRef = opportunity.GetAttributeValue<EntityReference>("vn_primarycontact");
            EntityReference accountRef = opportunity.GetAttributeValue<EntityReference>("vn_account");
            if (contactRef == null || accountRef == null)
                throw CrmPluginException.Create("CRM002", "An active Account and Primary Contact are required.");
            Entity account = service.Retrieve("account", accountRef.Id, new ColumnSet("statecode"));
            if ((account.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1) != 0)
                throw CrmPluginException.Create("CRM002", "The Opportunity Account must be active.");
            Entity contact = service.Retrieve("contact", contactRef.Id,
                new ColumnSet("statecode", "parentcustomerid"));
            if ((contact.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1) != 0)
                throw CrmPluginException.Create("CRM002", "The Primary Contact must be active.");
            EntityReference parent = contact.GetAttributeValue<EntityReference>("parentcustomerid");
            if (parent == null || parent.Id != accountRef.Id)
                throw CrmPluginException.Create("CRM002", "The Primary Contact must belong to the Opportunity Account.");

            if (targetStage >= Propose)
            {
                RequireText(opportunity, "vn_proposalsummary", "Proposal Summary");
                if (GetMoney(opportunity, "vn_total") <= 0m || !HasPositiveLine(service, opportunity.Id))
                    throw CrmPluginException.Create("CRM002", "Propose requires at least one positive Line and a positive Total.");
            }
            if (targetStage == Close &&
                !opportunity.Contains("vn_expectedclosedate"))
                throw CrmPluginException.Create("CRM002", "Expected Close Date is required before Close.");
        }

        private static bool HasPositiveLine(IOrganizationService service, Guid opportunityId)
        {
            var query = new QueryExpression("vn_opportunityline")
            {
                ColumnSet = new ColumnSet("vn_netamount"),
                TopCount = 1
            };
            query.Criteria.AddCondition("vn_opportunity", ConditionOperator.Equal, opportunityId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            query.Criteria.AddCondition("vn_netamount", ConditionOperator.GreaterThan, 0m);
            return service.RetrieveMultiple(query).Entities.Count > 0;
        }

        private static void ValidateCurrentApproval(IOrganizationService service, Entity opportunity,
            decimal total, decimal maxDiscount)
        {
            EntityReference requestRef = opportunity.GetAttributeValue<EntityReference>("vn_currentapproval");
            if (requestRef == null || requestRef.LogicalName != "vn_approvalrequest")
                throw CrmPluginException.Create("CRM022", "This Opportunity requires a current Approved Request before Won.");
            Entity request = service.Retrieve("vn_approvalrequest", requestRef.Id,
                new ColumnSet("vn_result", "vn_opportunity", "vn_dealowner", "vn_total", "vn_maxdiscount"));
            if ((request.GetAttributeValue<OptionSetValue>("vn_result")?.Value ?? -1) != 100000001 ||
                request.GetAttributeValue<EntityReference>("vn_opportunity")?.Id != opportunity.Id ||
                request.GetAttributeValue<EntityReference>("vn_dealowner")?.Id != opportunity.GetAttributeValue<EntityReference>("ownerid")?.Id ||
                request.GetAttributeValue<Money>("vn_total")?.Value != total ||
                GetDecimal(request, "vn_maxdiscount") != maxDiscount)
                throw CrmPluginException.Create("CRM022", "The current Approved Request does not match this Opportunity's owner and commercial snapshot.");
        }

        private static bool RequiresApproval(decimal total, decimal maxDiscount)
        {
            return total >= ApprovalTotalThreshold || maxDiscount > ApprovalDiscountThreshold;
        }

        private static void ValidateCloseDate(Entity opportunity, DateTime actualDate)
        {
            DateTime created = opportunity.GetAttributeValue<DateTime>("createdon").ToUniversalTime().Date;
            DateTime today = DateTime.UtcNow.Date;
            if (actualDate < created || actualDate > today)
                throw CrmPluginException.Create("CRM002", "Actual Close Date must be from the Created On UTC date through today.");
        }

        private static string ReadOptionalString(IPluginExecutionContext context, string name)
        {
            if (!context.InputParameters.Contains(name) || context.InputParameters[name] == null) return null;
            if (!(context.InputParameters[name] is string))
                throw CrmPluginException.Create("CRM002", name + " must be a String.");
            return (string)context.InputParameters[name];
        }

        private static string NormalizeRowVersion(string value)
        {
            if (value == null) return null;
            value = value.Trim();
            if (value.StartsWith("W/\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
                return value.Substring(3, value.Length - 4);
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private static decimal GetMoney(Entity entity, string attribute)
        {
            return entity.GetAttributeValue<Money>(attribute)?.Value ?? 0m;
        }

        private static decimal GetDecimal(Entity entity, string attribute)
        {
            if (!entity.Contains(attribute) || entity[attribute] == null) return 0m;
            if (entity[attribute] is decimal) return (decimal)entity[attribute];
            if (entity[attribute] is double) return Convert.ToDecimal((double)entity[attribute]);
            return Convert.ToDecimal(entity[attribute]);
        }

        private static void RequireText(Entity entity, string attribute, string label)
        {
            if (string.IsNullOrWhiteSpace(entity.GetAttributeValue<string>(attribute)))
                throw CrmPluginException.Create("CRM002", label + " is required for this stage.");
        }
    }
}
