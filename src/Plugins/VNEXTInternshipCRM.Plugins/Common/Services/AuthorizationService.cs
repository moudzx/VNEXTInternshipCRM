using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using VNEXTInternshipCRM.Plugins.Common.Exceptions;

namespace VNEXTInternshipCRM.Plugins.Common.Services
{
    public sealed class AuthorizationService
    {
        private const string SalesRepresentative = "Sales Representative";
        private const string SalesManager = "Sales Manager";

        private readonly IOrganizationService _service;

        public AuthorizationService(IOrganizationService service)
        {
            _service = service ??
                throw new ArgumentNullException(nameof(service));
        }

        public void ValidateLeadQualification(
            Guid initiatingUserId,
            Entity lead)
        {
            if (initiatingUserId == Guid.Empty ||
                lead == null ||
                lead.LogicalName != "vn_lead")
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The initiating user or Lead is invalid.");
            }

            Entity caller = RetrieveUser(initiatingUserId);

            if (caller.GetAttributeValue<bool>("isdisabled"))
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "Disabled users cannot qualify Leads.");
            }

            bool isRepresentative = HasRole(
                initiatingUserId,
                SalesRepresentative);

            bool isManager = HasRole(
                initiatingUserId,
                SalesManager);

            if (!isRepresentative && !isManager)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "Only Sales Representatives and Sales Managers can qualify Leads.");
            }

            EntityReference leadOwner =
                lead.GetAttributeValue<EntityReference>("ownerid");

            if (leadOwner == null ||
                leadOwner.LogicalName != "systemuser")
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The Lead must be owned by a user.");
            }

            Entity owner = RetrieveUser(leadOwner.Id);

            if (owner.GetAttributeValue<bool>("isdisabled"))
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The Lead owner is disabled.");
            }

            EntityReference callerBusinessUnit =
                caller.GetAttributeValue<EntityReference>("businessunitid");

            EntityReference ownerBusinessUnit =
                owner.GetAttributeValue<EntityReference>("businessunitid");

            if (callerBusinessUnit == null ||
                ownerBusinessUnit == null)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The caller or Lead owner has no Business Unit.");
            }

            if (callerBusinessUnit.Id != ownerBusinessUnit.Id)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The caller and Lead owner must belong to the same Business Unit.");
            }

            if (isRepresentative && leadOwner.Id != initiatingUserId)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "A Sales Representative can qualify only their own Lead.");
            }
        }

        private Entity RetrieveUser(Guid userId)
        {
            try
            {
                return _service.Retrieve(
                    "systemuser",
                    userId,
                    new ColumnSet(
                        "businessunitid",
                        "isdisabled"));
            }
            catch (Exception)
            {
                throw CrmPluginException.Create(
                    "CRM003",
                    "The user could not be verified.");
            }
        }

        public bool HasRole(Guid userId, string roleName)
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("name")
            };

            LinkEntity userRoles = query.AddLink(
                "systemuserroles",
                "roleid",
                "roleid",
                JoinOperator.Inner);

            userRoles.LinkCriteria.AddCondition(
                "systemuserid",
                ConditionOperator.Equal,
                userId);

            query.Criteria.AddCondition(
                "name",
                ConditionOperator.Equal,
                roleName);

            return _service.RetrieveMultiple(query).Entities.Count > 0;
        }
    }
}
