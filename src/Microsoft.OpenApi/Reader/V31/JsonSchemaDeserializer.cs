﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. 

using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Json.Schema.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader.ParseNodes;
using JsonSchema = Json.Schema.JsonSchema;

namespace Microsoft.OpenApi.Reader.V31
{
    /// <summary>
    /// Class containing logic to deserialize Open API V31 document into
    /// runtime Open API object model.
    /// </summary>
    internal static partial class OpenApiV31Deserializer
    {
        private static readonly FixedFieldMap<JsonSchemaBuilder> _schemaFixedFields = new()
        {
            {
                "title", (o, n, _) =>
                {
                    o.Title(n.GetScalarValue());
                }
            },
            {
                "multipleOf", (o, n, _) =>
                {
                    o.MultipleOf(decimal.Parse(n.GetScalarValue(), NumberStyles.Float, CultureInfo.InvariantCulture));
                }
            },
            {
                "maximum", (o, n, _) =>
                {
                    o.Maximum(decimal.Parse(n.GetScalarValue(), NumberStyles.Float, CultureInfo.InvariantCulture));
                }
            },
            {
                "exclusiveMaximum", (o, n, _) =>
                {
                    o.ExclusiveMaximum(decimal.Parse(n.GetScalarValue(), NumberStyles.Float, CultureInfo.InvariantCulture));
                }
            },
            {
                "minimum", (o, n, _) =>
                {
                    o.Minimum(decimal.Parse(n.GetScalarValue(), NumberStyles.Float, CultureInfo.InvariantCulture));
                }
            },
            {
                "exclusiveMinimum", (o, n, _) =>
                {
                    o.ExclusiveMinimum(decimal.Parse(n.GetScalarValue(), NumberStyles.Float, CultureInfo.InvariantCulture));
                }
            },
            {
                "maxLength", (o, n, _) =>
                {
                    o.MaxLength(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "minLength", (o, n, _) =>
                {
                    o.MinLength(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "pattern", (o, n, _) =>
                {
                    o.Pattern(n.GetScalarValue());
                }
            },
            {
                "maxItems", (o, n, _) =>
                {
                    o.MaxItems(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "minItems", (o, n, _) =>
                {
                    o.MinItems(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "uniqueItems", (o, n, _) =>
                {
                    o.UniqueItems(bool.Parse(n.GetScalarValue()));
                }
            },
            {
                "maxProperties", (o, n, _) =>
                {
                    o.MaxProperties(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "minProperties", (o, n, _) =>
                {
                    o.MinProperties(uint.Parse(n.GetScalarValue(), CultureInfo.InvariantCulture));
                }
            },
            {
                "required", (o, n, _) =>
                {
                    o.Required(new HashSet<string>(n.CreateSimpleList((n2, p) => n2.GetScalarValue())));
                }
            },
            {
                "enum", (o, n, _) =>
                {
                    o.Enum(n.CreateListOfAny());
                }
            },
            {
                "type", (o, n, _) =>
                {
                    if(n is ListNode)
                    {
                        o.Type(n.CreateSimpleList((s, p) => SchemaTypeConverter.ConvertToSchemaValueType(s.GetScalarValue())));
                    }
                    else
                    {
                        o.Type(SchemaTypeConverter.ConvertToSchemaValueType(n.GetScalarValue()));
                    }
                }
            },
            {
                "allOf", (o, n, t) =>
                {
                    o.AllOf(n.CreateList(LoadSchema, t));
                }
            },
            {
                "oneOf", (o, n, t) =>
                {
                    o.OneOf(n.CreateList(LoadSchema, t));
                }
            },
            {
                "anyOf", (o, n, t) =>
                {
                    o.AnyOf(n.CreateList(LoadSchema, t));
                }
            },
            {
                "not", (o, n, t) =>
                {
                    o.Not(LoadSchema(n, t));
                }
            },
            {
                "items", (o, n, t) =>
                {
                    o.Items(LoadSchema(n, t));
                }
            },
            {
                "properties", (o, n, t) =>
                {
                    o.Properties(n.CreateMap(LoadSchema, t));
                }
            },
            {
                "patternProperties", (o, n, t) =>
                {
                    o.PatternProperties(n.CreateMap(LoadSchema, t));
                }
            },
            {
                "additionalProperties", (o, n, t) =>
                {
                    if (n is ValueNode)
                    {
                        o.AdditionalProperties(bool.Parse(n.GetScalarValue()));
                    }
                    else
                    {
                        o.AdditionalProperties(LoadSchema(n, t));
                    }
                }
            },
            {
                "description", (o, n, _) =>
                {
                    o.Description(n.GetScalarValue());
                }
            },
            {
                "format", (o, n, _) =>
                {
                    o.Format(n.GetScalarValue());
                }
            },
            {
                "default", (o, n, _) =>
                {
                    o.Default(n.CreateAny().Node);
                }
            },
            {
                "discriminator", (o, n, t) =>
                {
                    var discriminator = LoadDiscriminator(n, t);
                    o.Discriminator(discriminator);
                }
            },
            {
                "readOnly", (o, n, _) =>
                {
                    o.ReadOnly(bool.Parse(n.GetScalarValue()));
                }
            },
            {
                "writeOnly", (o, n, _) =>
                {
                    o.WriteOnly(bool.Parse(n.GetScalarValue()));
                }
            },
            {
                "xml", (o, n, t) =>
                {
                    var xml = LoadXml(n);
                    o.Xml(xml.Namespace, xml.Name, xml.Prefix, xml.Attribute, xml.Wrapped,
                        (IReadOnlyDictionary<string, JsonNode>)xml.Extensions);
                }
            },
            {
                "externalDocs", (o, n, t) =>
                {
                   var externalDocs = LoadExternalDocs(n, t);
                   o.ExternalDocs(externalDocs.Url, externalDocs.Description,
                       (IReadOnlyDictionary<string, JsonNode>)externalDocs.Extensions);
                }
            },
            {
                "example", (o, n, _) =>
                {
                        o.Example(n.CreateAny().Node);
                }
            },
            {
                "examples", (o, n, _) =>
                    {
                        o.Examples(n.CreateSimpleList((s, p) =>(JsonNode) s.GetScalarValue()));
                    }
            },
            {
                "deprecated", (o, n, _) =>
                {
                    o.Deprecated(bool.Parse(n.GetScalarValue()));
                }
            },
        };

        private static readonly PatternFieldMap<JsonSchemaBuilder> _schemaPatternFields = new PatternFieldMap<JsonSchemaBuilder>
        {
            {s => s.StartsWith("x-"), (o, p, n, _) => o.Unrecognized(p, n.JsonNode)}
        };

        public static JsonSchema LoadSchema(ParseNode node, OpenApiDocument hostDocument = null)
        {
            Json.Schema.OpenApi.Vocabularies.Register();
            var schema = JsonSerializer.Deserialize<JsonSchema>(node.JsonNode);
            schema.BaseUri = hostDocument.BaseUri;
            return schema;
        }
    }

}
