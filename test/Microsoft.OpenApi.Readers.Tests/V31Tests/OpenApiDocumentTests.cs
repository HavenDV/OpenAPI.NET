﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Json.Schema;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Xunit;
using static System.Net.Mime.MediaTypeNames;

namespace Microsoft.OpenApi.Readers.Tests.V31Tests
{
    public class OpenApiDocumentTests
    {
        private const string SampleFolderPath = "V31Tests/Samples/OpenApiDocument/";

        public static T Clone<T>(T element) where T : IOpenApiSerializable
        {
            using var stream = new MemoryStream();
            IOpenApiWriter writer;
            var streamWriter = new FormattingStreamWriter(stream, CultureInfo.InvariantCulture);
            writer = new OpenApiJsonWriter(streamWriter, new OpenApiJsonWriterSettings()
            {
                InlineLocalReferences = true
            });
            element.SerializeAsV31(writer);
            writer.Flush();
            stream.Position = 0;

            using var streamReader = new StreamReader(stream);
            var result = streamReader.ReadToEnd();
            return new OpenApiStringReader().ReadFragment<T>(result, OpenApiSpecVersion.OpenApi3_1, out OpenApiDiagnostic diagnostic4);
        }

        [Fact]
        public void ParseDocumentWithWebhooksShouldSucceed()
        {
            // Arrange and Act
            using var stream = Resources.GetStream(Path.Combine(SampleFolderPath, "documentWithWebhooks.yaml"));
            var actual = new OpenApiStreamReader().Read(stream, out var diagnostic);

            var petSchema = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Required("id", "name")
                        .Properties(
                            ("id", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Integer)
                                .Format("int64")),
                            ("name", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                            ),
                            ("tag", new JsonSchemaBuilder().Type(SchemaValueType.String))
                        );

            var newPetSchema = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Required("name")
                        .Properties(
                            ("id", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Integer)
                                .Format("int64")),
                            ("name", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                            ),
                            ("tag", new JsonSchemaBuilder().Type(SchemaValueType.String))
                        );

            var components = new OpenApiComponents
            {
                Schemas =
                {
                    ["pet1"] = petSchema,
                    ["newPet"] = newPetSchema
                }
            };

            var expected = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Version = "1.0.0",
                    Title = "Webhook Example"
                },
                Webhooks = new Dictionary<string, OpenApiPathItem>
                {
                    ["/pets"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [OperationType.Get] = new OpenApiOperation
                            {
                                Description = "Returns all pets from the system that the user has access to",
                                OperationId = "findPets",
                                Parameters = new List<OpenApiParameter>
                                    {
                                        new OpenApiParameter
                                        {
                                            Name = "tags",
                                            In = ParameterLocation.Query,
                                            Description = "tags to filter by",
                                            Required = false,
                                            Schema = new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Array)
                                            .Items(new JsonSchemaBuilder()
                                                .Type(SchemaValueType.String)
                                            )
                                        },
                                        new OpenApiParameter
                                        {
                                            Name = "limit",
                                            In = ParameterLocation.Query,
                                            Description = "maximum number of results to return",
                                            Required = false,
                                            Schema = new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Integer).Format("int32")
                                        }
                                    },
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "pet response",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Array)
                                                    .Items(petSchema)

                                            },
                                            ["application/xml"] = new OpenApiMediaType
                                            {
                                                Schema = new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Array)
                                                    .Items(petSchema)
                                            }
                                        }
                                    }
                                }
                            },
                            [OperationType.Post] = new OpenApiOperation
                            {
                                RequestBody = new OpenApiRequestBody
                                {
                                    Description = "Information about a new pet in the system",
                                    Required = true,
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = newPetSchema
                                        }
                                    }
                                },
                                Responses = new OpenApiResponses
                                {
                                    ["200"] = new OpenApiResponse
                                    {
                                        Description = "Return a 200 status to indicate that the data was received successfully",
                                        Content = new Dictionary<string, OpenApiMediaType>
                                        {
                                            ["application/json"] = new OpenApiMediaType
                                            {
                                                Schema = petSchema
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Components = components
            };

            // Assert
            var schema = actual.Webhooks["/pets"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;
            diagnostic.Should().BeEquivalentTo(new OpenApiDiagnostic() { SpecificationVersion = OpenApiSpecVersion.OpenApi3_1 });
            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void ParseDocumentsWithReusablePathItemInWebhooksSucceeds()
        {
            // Arrange && Act
            using var stream = Resources.GetStream("V31Tests/Samples/OpenApiDocument/documentWithReusablePaths.yaml");
            var actual = new OpenApiStreamReader().Read(stream, out var context);

            var components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, JsonSchema>
                {
                    ["petSchema"] = new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Required("id", "name")
                                .Properties(
                                    ("id", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Format("int64")),
                                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("tag", new JsonSchemaBuilder().Type(SchemaValueType.String))),
                    ["newPet"] = new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Required("name")
                                .Properties(
                                    ("id", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Format("int64")),
                                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("tag", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                }
            };

            // Create a clone of the schema to avoid modifying things in components.
            var petSchema = components.Schemas["petSchema"];
            var newPetSchema = components.Schemas["newPet"];

            components.PathItems = new Dictionary<string, OpenApiPathItem>
            {
                ["/pets"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Get] = new OpenApiOperation
                        {
                            Description = "Returns all pets from the system that the user has access to",
                            OperationId = "findPets",
                            Parameters = new List<OpenApiParameter>
                                {
                                    new OpenApiParameter
                                    {
                                        Name = "tags",
                                        In = ParameterLocation.Query,
                                        Description = "tags to filter by",
                                        Required = false,
                                        Schema = new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Array)
                                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                                    },
                                    new OpenApiParameter
                                    {
                                        Name = "limit",
                                        In = ParameterLocation.Query,
                                        Description = "maximum number of results to return",
                                        Required = false,
                                        Schema = new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Integer).Format("int32")
                                    }
                                },
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "pet response",
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = new JsonSchemaBuilder()
                                                .Type(SchemaValueType.Array)
                                                .Items(petSchema)
                                        },
                                        ["application/xml"] = new OpenApiMediaType
                                        {
                                            Schema = new JsonSchemaBuilder()
                                                .Type(SchemaValueType.Array)
                                                .Items(petSchema)
                                        }
                                    }
                                }
                            }
                        },
                        [OperationType.Post] = new OpenApiOperation
                        {
                            RequestBody = new OpenApiRequestBody
                            {
                                Description = "Information about a new pet in the system",
                                Required = true,
                                Content = new Dictionary<string, OpenApiMediaType>
                                {
                                    ["application/json"] = new OpenApiMediaType
                                    {
                                        Schema = newPetSchema
                                    }
                                }
                            },
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "Return a 200 status to indicate that the data was received successfully",
                                    Content = new Dictionary<string, OpenApiMediaType>
                                    {
                                        ["application/json"] = new OpenApiMediaType
                                        {
                                            Schema = petSchema
                                        },
                                    }
                                }
                            }
                        }
                    },
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.PathItem,
                        Id = "/pets",
                        HostDocument = actual
                    }
                }
            };

            var expected = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Title = "Webhook Example",
                    Version = "1.0.0"
                },
                JsonSchemaDialect = "http://json-schema.org/draft-07/schema#",
                Webhooks = components.PathItems,
                Components = components
            };

            // Assert
            actual.Should().BeEquivalentTo(expected);
            context.Should().BeEquivalentTo(
    new OpenApiDiagnostic() { SpecificationVersion = OpenApiSpecVersion.OpenApi3_1 });
        }

        [Fact]
        public void ParseDocumentWithDescriptionInDollarRefsShouldSucceed()
        {
            // Arrange
            using var stream = Resources.GetStream(Path.Combine(SampleFolderPath, "documentWithSummaryAndDescriptionInReference.yaml"));

            // Act
            var actual = new OpenApiStreamReader().Read(stream, out var diagnostic);
            var schema = actual.Paths["/pets"].Operations[OperationType.Get].Responses["200"].Content["application/json"].Schema;
            var header = actual.Components.Responses["Test"].Headers["X-Test"];

            // Assert
            Assert.True(header.Description == "A referenced X-Test header"); /*response header #ref's description overrides the header's description*/
            Assert.Null(schema.GetRef());
            Assert.Equal(SchemaValueType.Object, schema.GetJsonType());
            Assert.Equal("A pet in a petstore", schema.GetDescription()); /*The reference object's description overrides that of the referenced component*/
        }
    }
}
