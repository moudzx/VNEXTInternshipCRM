using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using VNEXTInternshipCRM.Plugins.Common.Exceptions;

namespace VNEXTInternshipCRM.Plugins.Common.Services
{
    /// <summary>Writes only the generated Opportunity BPF instance using the configured plug-in identity.</summary>
    public sealed class BusinessProcessFlowService
    {
        private const string BpfEntity = "vn_opportunitysalesprocess";
        private static readonly Guid ProcessId = new Guid("dd6dd9e5-ade8-f111-84e7-7ced8d420da8");
        private static readonly Guid[] StageIds =
        {
            new Guid("dc367bde-1cf8-4aba-abb3-844984cdc43d"),
            new Guid("e77f5cf0-7b0e-4b88-bcec-f45705a7cfa3"),
            new Guid("0103458a-9eae-47e4-adee-10df924f11d4"),
            new Guid("2884d336-4b87-4519-9663-6c9f894ed38d")
        };
        private readonly IOrganizationService service;

        public BusinessProcessFlowService(IOrganizationService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Guid CreateForOpportunity(Guid opportunityId)
        {
            if (opportunityId == Guid.Empty) throw CrmPluginException.Create("CRM001", "A valid Opportunity ID is required.");
            if (FindInstances(opportunityId).Count != 0)
                throw CrmPluginException.Create("CRM005", "An Opportunity BPF instance already exists; duplicate creation is refused.");
            var row = new Entity(BpfEntity);
            row["bpf_name"] = "Opportunity Sales Process";
            row["processid"] = new EntityReference("workflow", ProcessId);
            row["activestageid"] = new EntityReference("processstage", StageIds[0]);
            row["bpf_vn_opportunityid"] = new EntityReference("vn_opportunity", opportunityId);
            row["traversedpath"] = StageIds[0].ToString("D");
            row["activestagestartedon"] = DateTime.UtcNow.Date;
            return service.Create(row);
        }

        public void Advance(Guid opportunityId, int stageValue)
        {
            if (stageValue < 100000001 || stageValue > 100000003)
                throw CrmPluginException.Create("CRM002", "The requested BPF stage is invalid.");
            Entity row = GetSingleInstance(opportunityId);
            int index = stageValue - 100000000;
            Guid expectedCurrent = StageIds[index - 1];
            if (row.GetAttributeValue<EntityReference>("activestageid")?.Id != expectedCurrent)
                throw CrmPluginException.Create("CRM005", "The BPF is not at the expected preceding stage.");
            string path = row.GetAttributeValue<string>("traversedpath");
            string next = StageIds[index].ToString("D");
            string expected = expectedCurrent.ToString("D");
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(expected, StringComparison.OrdinalIgnoreCase))
                throw CrmPluginException.Create("CRM005", "The BPF traversed path is inconsistent with the Opportunity stage.");
            var update = new Entity(BpfEntity, row.Id);
            update["activestageid"] = new EntityReference("processstage", StageIds[index]);
            update["traversedpath"] = path + "," + next;
            update["activestagestartedon"] = DateTime.UtcNow.Date;
            service.Update(update);
        }

        public void Finish(Guid opportunityId) { CloseInstance(opportunityId, 1, 2); }
        public void Abort(Guid opportunityId) { CloseInstance(opportunityId, 1, 3); }

        private void CloseInstance(Guid opportunityId, int state, int status)
        {
            Entity row = GetSingleInstance(opportunityId);
            var update = new Entity(BpfEntity, row.Id);
            update["statecode"] = new OptionSetValue(state);
            update["statuscode"] = new OptionSetValue(status);
            update["completedon"] = DateTime.UtcNow.Date;
            service.Update(update);
        }

        private Entity GetSingleInstance(Guid opportunityId)
        {
            List<Entity> rows = FindInstances(opportunityId);
            if (rows.Count != 1)
                throw CrmPluginException.Create("CRM005", "Exactly one Opportunity BPF instance is required; found " + rows.Count + ".");
            Entity row = rows[0];
            if (row.GetAttributeValue<EntityReference>("processid")?.Id != ProcessId)
                throw CrmPluginException.Create("CRM005", "The Opportunity BPF instance references an unexpected process.");
            if ((row.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? -1) != 0 ||
                (row.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? -1) != 1)
                throw CrmPluginException.Create("CRM005", "The Opportunity BPF instance is not Active.");
            return row;
        }

        private List<Entity> FindInstances(Guid opportunityId)
        {
            var query = new QueryExpression(BpfEntity)
            {
                ColumnSet = new ColumnSet("processid", "activestageid", "traversedpath", "statecode", "statuscode"),
                TopCount = 2
            };
            query.Criteria.AddCondition("bpf_vn_opportunityid", ConditionOperator.Equal, opportunityId);
            return new List<Entity>(service.RetrieveMultiple(query).Entities);
        }
    }
}
