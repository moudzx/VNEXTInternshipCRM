using System;
using VNEXTInternshipCRM.Plugins.Common.Context;

namespace VNEXTInternshipCRM.Plugins.Infrastructure
{
    public abstract class CrmPluginBase : PluginBase
    {
        protected CrmPluginBase(Type pluginType)
            : base(pluginType)
        {
        }

        protected override void ExecuteDataversePlugin(
            ILocalPluginContext localContext)
        {
            if (localContext == null)
                throw new ArgumentNullException(nameof(localContext));

            var context = new PluginExecutionContext(localContext);

            context.Tracing.Trace(
                "Executing {0}. CorrelationId: {1}. InitiatingUser: {2}. PluginUser: {3}.",
                PluginClassName,
                context.CorrelationId,
                context.InitiatingUserId,
                context.PluginUserId);

            Execute(context);
        }

        protected abstract void Execute(
            PluginExecutionContext context);
    }
}