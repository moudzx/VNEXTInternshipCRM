using System;
using Microsoft.Xrm.Sdk;

namespace VNEXTInternshipCRM.Plugins.Common.Validation
{
    public static class PluginValidator
    {
        public static T RequireInput<T>(
            IPluginExecutionContext context,
            string parameterName)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (string.IsNullOrWhiteSpace(parameterName))
                throw new ArgumentException(
                    "Parameter name cannot be empty.",
                    nameof(parameterName));

            if (!context.InputParameters.Contains(parameterName))
            {
                throw new InvalidPluginExecutionException(
                    $"Required parameter '{parameterName}' is missing.");
            }

            object value = context.InputParameters[parameterName];

            if (!(value is T))
            {
                throw new InvalidPluginExecutionException(
                    $"Parameter '{parameterName}' has an invalid type. " +
                    $"Expected {typeof(T).Name}.");
            }

            return (T)value;
        }

        public static void RequireNonEmptyString(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidPluginExecutionException(
                    $"'{parameterName}' cannot be empty.");
            }
        }
    }
}