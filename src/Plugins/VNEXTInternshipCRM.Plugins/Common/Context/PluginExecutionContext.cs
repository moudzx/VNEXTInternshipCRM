using System;
using Microsoft.Xrm.Sdk;

namespace VNEXTInternshipCRM.Plugins.Common.Context
{
    public sealed class PluginExecutionContext
    {
        public IPluginExecutionContext Context { get; }

        // Operations performed with the actual initiating user's permissions.
        public IOrganizationService Service { get; }

        // Identity configured on the plug-in registration.
        public IOrganizationService PluginUserService { get; }

        public IOrganizationServiceFactory ServiceFactory { get; }

        public ITracingService Tracing { get; }

        public Guid InitiatingUserId => Context.InitiatingUserId;

        public Guid PluginUserId => Context.UserId;

        public Guid CorrelationId => Context.CorrelationId;

        public bool IsInTransaction => Context.IsInTransaction;

        public PluginExecutionContext(ILocalPluginContext localContext)
        {
            if (localContext == null)
                throw new ArgumentNullException(nameof(localContext));

            Context = localContext.PluginExecutionContext;
            Service = localContext.InitiatingUserService;
            PluginUserService = localContext.PluginUserService;
            ServiceFactory = localContext.OrgSvcFactory;
            Tracing = localContext.TracingService;

            if (Context == null)
                throw new InvalidPluginExecutionException(
                    "Dataverse execution context is unavailable.");

            if (Service == null)
                throw new InvalidPluginExecutionException(
                    "Initiating user service is unavailable.");

            if (ServiceFactory == null)
                throw new InvalidPluginExecutionException(
                    "Organization service factory is unavailable.");

            if (Tracing == null)
                throw new InvalidPluginExecutionException(
                    "Tracing service is unavailable.");
        }

        public void RequireTransaction()
        {
            if (!IsInTransaction)
            {
                throw new InvalidPluginExecutionException(
                    "This operation must execute inside a Dataverse transaction.");
            }
        }

        public IOrganizationService CreateScopedService(Guid serviceUserId)
        {
            if (serviceUserId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    "A valid configured service identity is required.");
            }

            return ServiceFactory.CreateOrganizationService(serviceUserId);
        }
    }
}