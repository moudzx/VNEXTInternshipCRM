
using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace VNEXTInternshipCRM.Plugins.Common.Services
{
    public sealed class DataverseService
    {
        private readonly IOrganizationService _service;

        public DataverseService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Entity Retrieve(
            string logicalName,
            Guid id,
            params string[] columns)
        {
            return _service.Retrieve(
                logicalName,
                id,
                new ColumnSet(columns));
        }

        public Guid Create(Entity entity)
        {
            return _service.Create(entity);
        }

        public void Update(Entity entity)
        {
            _service.Update(entity);
        }

        public EntityReference RetrieveBaseCurrency()
        {
            var query = new QueryExpression("organization")
            {
                ColumnSet = new ColumnSet("basecurrencyid"),
                TopCount = 1
            };

            EntityCollection organizations =
                _service.RetrieveMultiple(query);

            if (organizations.Entities.Count == 0)
            {
                throw new InvalidPluginExecutionException(
                    "CRM009: The organization base currency could not be retrieved.");
            }

            EntityReference currency =
                organizations.Entities[0]
                    .GetAttributeValue<EntityReference>("basecurrencyid");

            if (currency == null)
            {
                throw new InvalidPluginExecutionException(
                    "CRM009: The organization has no configured base currency.");
            }

            return currency;
        }

        public void UpdateIfRowVersionMatches(Entity entity)
        {
            if (entity == null ||
                string.IsNullOrWhiteSpace(entity.RowVersion))
            {
                throw new InvalidPluginExecutionException(
                    "CRM021: A valid RowVersion is required.");
            }

            var request = new UpdateRequest
            {
                Target = entity,
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
            };

            _service.Execute(request);
        }
    }
}
