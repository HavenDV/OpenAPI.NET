﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Json.Schema;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace Microsoft.OpenApi.Services
{
    /// <summary>
    /// Contains a set of OpenApi documents and document fragments that reference each other
    /// </summary>
    public class OpenApiWorkspace
    {
        private readonly Dictionary<Uri, OpenApiDocument> _documents = new();
        private readonly Dictionary<Uri, IOpenApiReferenceable> _fragments = new();
        private readonly Dictionary<Uri, JsonSchema> _schemaFragments = new();
        private readonly Dictionary<Uri, Stream> _artifacts = new();
        private IDictionary<string, IOpenApiReferenceable> _referenceableRegistry = new Dictionary<string, IOpenApiReferenceable>();
        private IDictionary<string, IBaseDocument> _schemaRegistry = new Dictionary<string, IBaseDocument>();


        /// <summary>
        /// A list of OpenApiDocuments contained in the workspace
        /// </summary>
        public IEnumerable<OpenApiDocument> Documents
        {
            get
            {
                return _documents.Values;
            }
        }

        /// <summary>
        /// A list of document fragments that are contained in the workspace
        /// </summary>
        public IEnumerable<IOpenApiElement> Fragments { get; }

        /// <summary>
        /// The base location from where all relative references are resolved
        /// </summary>
        public Uri BaseUrl { get; }

        /// <summary>
        /// A list of document fragments that are contained in the workspace
        /// </summary>
        public IEnumerable<Stream> Artifacts { get; }

        /// <summary>
        /// Initialize workspace pointing to a base URL to allow resolving relative document locations.  Use a file:// url to point to a folder
        /// </summary>
        /// <param name="baseUrl"></param>
        public OpenApiWorkspace(Uri baseUrl)
        {
            BaseUrl = baseUrl;
        }

        /// <summary>
        /// Initialize workspace using current directory as the default location.
        /// </summary>
        public OpenApiWorkspace()
        {
            BaseUrl = new("file://" + Environment.CurrentDirectory + $"{Path.DirectorySeparatorChar}" );
        }

        /// <summary>
        /// Initializes a copy of an <see cref="OpenApiWorkspace"/> object
        /// </summary>
        public OpenApiWorkspace(OpenApiWorkspace workspace) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="baseDocument"></param>
        public void RegisterComponent(Uri uri, IBaseDocument baseDocument)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (baseDocument == null) throw new ArgumentNullException(nameof(baseDocument));

            if (_schemaRegistry.ContainsKey(uri.ToString()))
            {
                throw new InvalidOperationException($"Key already exists. {nameof(uri)} needs to be unique");
            }
            else
            {
                _schemaRegistry.Add(uri.OriginalString, baseDocument);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="referenceable"></param>
        public void RegisterComponent(Uri uri, IOpenApiReferenceable referenceable)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (referenceable == null) throw new ArgumentNullException(nameof(referenceable));

            if (_referenceableRegistry.ContainsKey(uri.OriginalString))
            {
                throw new InvalidOperationException($"Key already exists. {nameof(uri)} needs to be unique");
            }
            else
            {
                _referenceableRegistry.Add(uri.OriginalString, referenceable);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="uri"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryRetrieveComponent<TValue>(Uri uri, out TValue value)
        {
            if (uri == null)
            {
                value = default;
                return false;
            }
            
            if ((typeof(TValue) == typeof(IBaseDocument)))
            {
                _schemaRegistry.TryGetValue(uri.OriginalString, out IBaseDocument schema);
                if (schema != null)
                {
                    value = (TValue)schema;
                    return true;
                }
            }
            else if(typeof(TValue) == typeof(IOpenApiReferenceable))
            {
                _referenceableRegistry.TryGetValue(uri.OriginalString, out IOpenApiReferenceable referenceable);
                if (referenceable != null)
                {
                    value = (TValue)referenceable;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Verify if workspace contains a document based on its URL.
        /// </summary>
        /// <param name="location">A relative or absolute URL of the file.  Use file:// for folder locations.</param>
        /// <returns>Returns true if a matching document is found.</returns>
        public bool Contains(string location)
        {
            var key = ToLocationUrl(location);
            return _documents.ContainsKey(key) || _fragments.ContainsKey(key) || _artifacts.ContainsKey(key);
        }

        /// <summary>
        /// Add an OpenApiDocument to the workspace.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="document"></param>
        public void AddDocument(string location, OpenApiDocument document)
        {
            document.Workspace = this;
            _documents.Add(ToLocationUrl(location), document);
        }

        /// <summary>
        /// Adds a fragment of an OpenApiDocument to the workspace.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="fragment"></param>
        /// <remarks>Not sure how this is going to work.  Does the reference just point to the fragment as a whole, or do we need to
        /// to be able to point into the fragment.  Keeping it private until we figure it out.
        /// </remarks>
        public void AddFragment(string location, IOpenApiReferenceable fragment)
        {
            _fragments.Add(ToLocationUrl(location), fragment);
        }

        /// <summary>
        /// Adds a schema fragment of an OpenApiDocument to the workspace.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="fragment"></param>
        public void AddSchemaFragment(string location, JsonSchema fragment)
        {
            _schemaFragments.Add(ToLocationUrl(location), fragment);
        }

        /// <summary>
        /// Add a stream based artificat to the workspace.  Useful for images, examples, alternative schemas.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="artifact"></param>
        public void AddArtifact(string location, Stream artifact)
        {
            _artifacts.Add(ToLocationUrl(location), artifact);
        }

        /// <summary>
        /// Returns the target of an OpenApiReference from within the workspace.
        /// </summary>
        /// <param name="reference">An instance of an OpenApiReference</param>
        /// <returns></returns>
        public IOpenApiReferenceable ResolveReference(OpenApiReference reference)
        {
            if (_documents.TryGetValue(new(BaseUrl, reference.ExternalResource), out var doc))
            {
                return doc.ResolveReference(reference, false);
            }
            else if (_fragments.TryGetValue(new(BaseUrl, reference.ExternalResource), out var fragment))
            {
                var jsonPointer = new JsonPointer($"/{reference.Id ?? string.Empty}");
                return fragment.ResolveReference(jsonPointer);
            }
            return null;
        }

        /// <summary>
        /// Resolve the target of a JSON schema reference from within the workspace
        /// </summary>
        /// <param name="reference">An instance of a JSON schema reference.</param>
        /// <returns></returns>
        public JsonSchema ResolveJsonSchemaReference(Uri reference)
        {
            var docs = _documents.Values;
            if (docs.Any())
            {
                var doc = docs.FirstOrDefault();
                if (doc != null)
                {
                    foreach (var jsonSchema in doc.Components.Schemas)
                    {
                        var refUri = new Uri(OpenApiConstants.V3ReferenceUri + jsonSchema.Key);
                        SchemaRegistry.Global.Register(refUri, jsonSchema.Value);
                    }

                    var resolver = new OpenApiReferenceResolver(doc);
                    return resolver.ResolveJsonSchemaReference(reference);
                }
                return null;
            }
            else
            {
                foreach (var jsonSchema in _schemaFragments)
                {
                    SchemaRegistry.Global.Register(reference, jsonSchema.Value);
                }

                return FetchSchemaFromRegistry(reference);                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public Stream GetArtifact(string location)
        {
            return _artifacts[ToLocationUrl(location)];
        }

        private Uri ToLocationUrl(string location)
        {
            return new(BaseUrl, location);
        }

        private static JsonSchema FetchSchemaFromRegistry(Uri reference)
        {
            var resolvedSchema = (JsonSchema)SchemaRegistry.Global.Get(reference);
            return resolvedSchema;
        }
    }
}
