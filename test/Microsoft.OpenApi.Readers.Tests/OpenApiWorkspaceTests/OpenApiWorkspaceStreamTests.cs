﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Json.Schema;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers.Interface;
using Xunit;

namespace Microsoft.OpenApi.Readers.Tests.OpenApiWorkspaceTests
{
    public class OpenApiWorkspaceStreamTests
    {
        private const string SampleFolderPath = "V3Tests/Samples/OpenApiWorkspace/";

        // Use OpenApiWorkspace to load a document and a referenced document

        [Fact]
        public async Task LoadingDocumentWithResolveAllReferencesShouldLoadDocumentIntoWorkspace()
        {
            // Create a reader that will resolve all references
            var reader = new OpenApiStreamReader(new OpenApiReaderSettings()
            {
                LoadExternalRefs = true,
                CustomExternalLoader = new MockLoader(),
                BaseUrl = new Uri("file://c:\\")
            });

            // Todo: this should be ReadAsync
            var stream = new MemoryStream();
            var doc = @"openapi: 3.0.0
info:
  title: foo
  version: 1.0.0
paths: {}";
            var wr = new StreamWriter(stream);
            wr.Write(doc);
            wr.Flush();
            stream.Position = 0;

            var result = await reader.ReadAsync(stream);

            Assert.NotNull(result.OpenApiDocument.Workspace);

        }


        [Fact]
        public async Task LoadDocumentWithExternalReferenceShouldLoadBothDocumentsIntoWorkspace()
        {
            // Create a reader that will resolve all references
            var reader = new OpenApiStreamReader(new OpenApiReaderSettings()
            {
                LoadExternalRefs = true,
                CustomExternalLoader = new ResourceLoader(),
                BaseUrl = new Uri("file://c:\\")
            });

            ReadResult result;
            using var stream = Resources.GetStream("V3Tests/Samples/OpenApiWorkspace/TodoMain.yaml");
            result = await reader.ReadAsync(stream);
            

            Assert.NotNull(result.OpenApiDocument.Workspace);
            Assert.True(result.OpenApiDocument.Workspace.Contains("TodoComponents.yaml"));

            var referencedSchema = result.OpenApiDocument
                                    .Paths["/todos"]
                                    .Operations[OperationType.Get]
                                    .Responses["200"]
                                    .Content["application/json"]
                                    .Schema;

            var x = referencedSchema.GetProperties().TryGetValue("subject", out var schema);
            Assert.Equal(SchemaValueType.Object, referencedSchema.GetJsonType());
            Assert.Equal(SchemaValueType.String, schema.GetJsonType());

            var referencedParameter = result.OpenApiDocument
                                        .Paths["/todos"]
                                        .Operations[OperationType.Get]
                                        .Parameters.Select(p => p.GetEffective(result.OpenApiDocument))
                                        .FirstOrDefault(p => p.Name == "filter");

            Assert.Equal(SchemaValueType.String, referencedParameter.Schema.GetJsonType());

        }
    }

    public class MockLoader : IStreamLoader
    {
        public Stream Load(Uri uri)
        {
            return null;
        }

        public Task<Stream> LoadAsync(Uri uri)
        {
            return null;
        }
    }


    public class ResourceLoader : IStreamLoader
    {
        public Stream Load(Uri uri)
        {
            return null;
        }

        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = new Uri(new Uri("http://example.org/V3Tests/Samples/OpenApiWorkspace/"), uri).AbsolutePath;
            path = path.Substring(1); // remove leading slash
            return Task.FromResult(Resources.GetStream(path));
        }
    }
}
