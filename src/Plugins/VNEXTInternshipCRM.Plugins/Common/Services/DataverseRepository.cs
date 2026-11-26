using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace VNEXTInternshipCRM.Plugins.Common.Services
{
    public sealed class DataverseRepository
    {
        private readonly IOrganizationService _service;

        public DataverseRepository(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Entity Retrieve(
            string logicalName,
            Guid id,
            params string[] columns)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
                throw new ArgumentException(
                    "Entity logical name is required.",
                    nameof(logicalName));

            if (id == Guid.Empty)
                throw new ArgumentException(
                    "Record ID cannot be empty.",
                    nameof(id));

            return _service.Retrieve(
                logicalName,
                id,
                new ColumnSet(columns));
        }

        public Guid Create(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return _service.Create(entity);
        }

        public void Update(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _service.Update(entity);
        }

        public void UpdateIfRowVersionMatches(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (string.IsNullOrWhiteSpace(entity.RowVersion))
            {
                throw new InvalidPluginExecutionException(
                    "A RowVersion is required for this update.");
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