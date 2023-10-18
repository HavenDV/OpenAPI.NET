﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. 

using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers.ParseNodes;

namespace Microsoft.OpenApi.Readers.V31
{
    /// <summary>
    /// Class containing logic to deserialize Open API V31 document into
    /// runtime Open API object model.
    /// </summary>
    internal static partial class OpenApiV31Deserializer
    {
        public static OpenApiSecurityRequirement LoadSecurityRequirement(ParseNode node)
        {
            var mapNode = node.CheckMapNode("security");
            string description = null;
            string summary = null;

            var securityRequirement = new OpenApiSecurityRequirement();

            foreach (var property in mapNode)
            {
                if (property.Name.Equals("description") || property.Name.Equals("summary"))
                {
                    description = node.Context.VersionService.GetReferenceScalarValues(mapNode, OpenApiConstants.Description);
                    summary = node.Context.VersionService.GetReferenceScalarValues(mapNode, OpenApiConstants.Summary);
                }

                var scheme = LoadSecuritySchemeByReference(property.Name, summary, description);

                var scopes = property.Value.CreateSimpleList(value => value.GetScalarValue());

                if (scheme != null)
                {
                    securityRequirement.Add(scheme, scopes);
                }
                else
                {
                    mapNode.Context.Diagnostic.Errors.Add(
                        new OpenApiError(node.Context.GetLocation(), $"Scheme {property.Name} is not found"));
                }
            }

            return securityRequirement;
        }

        private static OpenApiSecurityScheme LoadSecuritySchemeByReference(
            string schemeName,
            string summary = null,
            string description = null)
        {
            var securitySchemeObject = new OpenApiSecurityScheme()
            {
                UnresolvedReference = true,
                Reference = new OpenApiReference()
                {
                    Summary = summary,
                    Description = description,
                    Id = schemeName,
                    Type = ReferenceType.SecurityScheme
                }
            };

            return securitySchemeObject;
        }
    }
}
