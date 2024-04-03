﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Linq;
using FluentAssertions;
using Json.Schema;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Xunit;

namespace Microsoft.OpenApi.Readers.Tests.V3Tests
{
    public class OpenApiOperationTests
    {
        private const string SampleFolderPath = "V3Tests/Samples/OpenApiOperation/";

        public OpenApiOperationTests()
        {
            OpenApiReaderRegistry.RegisterReader("yaml", new OpenApiYamlReader());
        }

        [Fact]
        public void OperationWithSecurityRequirementShouldReferenceSecurityScheme()
        {
            var result = OpenApiDocument.Load(Path.Combine(SampleFolderPath, "securedOperation.yaml"));

            var securityRequirement = result.OpenApiDocument.Paths["/"].Operations[OperationType.Get].Security.First();

            Assert.Same(securityRequirement.Keys.First(), result.OpenApiDocument.Components.SecuritySchemes.First().Value);
        }

        [Fact]
        public void ParseOperationWithParameterWithNoLocationShouldSucceed()
        {
            // Act
            var operation = OpenApiModelFactory.Load<OpenApiOperation>(Path.Combine(SampleFolderPath, "operationWithParameterWithNoLocation.json"), OpenApiSpecVersion.OpenApi3_0, out _);

            // Assert
            operation.Should().BeEquivalentTo(new OpenApiOperation
            {
                Tags =
                {
                    new OpenApiTag
                    {
                        UnresolvedReference = true,
                        Reference = new()
                        {
                            Id = "user",
                            Type = ReferenceType.Tag
                        }
                    }
                },
                Summary = "Logs user into the system",
                Description = "",
                OperationId = "loginUser",
                Parameters =
                {
                    new OpenApiParameter
                    {
                        Name = "username",
                        Description = "The user name for login",
                        Required = true,
                        Schema = new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                    },
                    new OpenApiParameter
                    {
                        Name = "password",
                        Description = "The password for login in clear text",
                        In = ParameterLocation.Query,
                        Required = true,
                        Schema = new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                    }
                }
            });
        }
    }
}
