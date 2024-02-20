﻿using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers.ParseNodes;
using Microsoft.OpenApi.Readers.V2;

namespace Microsoft.OpenApi.Readers.V31
{
    /// <summary>
    /// Class containing logic to deserialize Open API V31 document into
    /// runtime Open API object model.
    /// </summary>
    internal static partial class OpenApiV31Deserializer
    {
        private static readonly FixedFieldMap<OpenApiDocument> _openApiFixedFields = new()
        {
            {
                "openapi", (o, n) =>
                {
                } /* Version is valid field but we already parsed it */
            },
            {"info", (o, n) => o.Info = LoadInfo(n)},
            {"jsonSchemaDialect", (o, n) => o.JsonSchemaDialect = n.GetScalarValue() },
            {"servers", (o, n) => n.Context.SetTempStorage(TempStorageKeys.Servers, o.Servers = n.CreateList(LoadServer))},
            {"paths", (o, n) => o.Paths = LoadPaths(n)},
            {"webhooks", (o, n) => o.Webhooks = LoadPaths(n)},
            {"components", (o, n) => o.Components = LoadComponents(n)},
            {"tags", (o, n) => {o.Tags = n.CreateList(LoadTag);
                foreach (var tag in o.Tags)
    {
                    tag.Reference = new OpenApiReference()
                    {
                        Id = tag.Name,
                        Type = ReferenceType.Tag
                    };
    }
            } },
            {"externalDocs", (o, n) => o.ExternalDocs = LoadExternalDocs(n)},
            {"security", (o, n) => o.SecurityRequirements = n.CreateList(LoadSecurityRequirement)}
        };

        private static readonly PatternFieldMap<OpenApiDocument> _openApiPatternFields = new()
        {
            // We have no semantics to verify X- nodes, therefore treat them as just values.
            {s => s.StartsWith("x-"), (o, p, n) => o.AddExtension(p, LoadExtension(p, n))}
        };

        public static OpenApiDocument LoadOpenApi(RootNode rootNode)
        {
            var openApidoc = new OpenApiDocument();
            var openApiNode = rootNode.GetMap();

            ParseMap(openApiNode, openApidoc, _openApiFixedFields, _openApiPatternFields);

            return openApidoc;
        }
    }
}
