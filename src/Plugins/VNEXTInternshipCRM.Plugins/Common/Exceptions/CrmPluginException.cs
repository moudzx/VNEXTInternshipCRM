using Microsoft.Xrm.Sdk;

namespace VNEXTInternshipCRM.Plugins.Common.Exceptions
{
    public static class CrmPluginException
    {
        public static InvalidPluginExecutionException Create(
            string errorCode,
            string message)
        {
            return new InvalidPluginExecutionException(
                $"{errorCode}: {message}");
        }
    }
}