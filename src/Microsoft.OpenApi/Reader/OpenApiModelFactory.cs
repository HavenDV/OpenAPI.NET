﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace Microsoft.OpenApi.Reader
{
    /// <summary>
    /// A factory class for loading OpenAPI models from various sources.
    /// </summary>
    public static class OpenApiModelFactory
    {
        private static readonly HttpClient _httpClient = new();

        static OpenApiModelFactory()
        {
            OpenApiReaderRegistry.RegisterReader(OpenApiConstants.Json, new OpenApiJsonReader());
        }

        /// <summary>
        /// Loads the input URL and parses it into an Open API document.
        /// </summary>
        /// <param name="url">The path to the OpenAPI file.</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <returns>An OpenAPI document instance.</returns>
        public static ReadResult Load(string url, OpenApiReaderSettings settings = null)
        {
            return LoadAsync(url, settings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads the input stream and parses it into an Open API document.
        /// </summary>
        /// <param name="stream"> The input stream.</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <param name="format">The OpenAPI format.</param>
        /// <returns>An OpenAPI document instance.</returns>
        public static ReadResult Load(Stream stream,
                                      string format,
                                      OpenApiReaderSettings settings = null)
        {
            settings ??= new OpenApiReaderSettings();

            var result = LoadAsync(stream, format, settings).GetAwaiter().GetResult();
            if (!settings.LeaveStreamOpen)
            {
                stream.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Loads the TextReader input and parses it into an Open API document.
        /// </summary>
        /// <param name="input">The TextReader input.</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <param name="format">The Open API format</param>
        /// <returns>An OpenAPI document instance.</returns>
        public static ReadResult Load(TextReader input,
                                      string format,
                                      OpenApiReaderSettings settings = null)
        {
            return LoadAsync(input, format, settings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Loads the input URL and parses it into an Open API document.
        /// </summary>
        /// <param name="url">The path to the OpenAPI file</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <returns></returns>
        public static async Task<ReadResult> LoadAsync(string url, OpenApiReaderSettings settings = null)
        {
            var format = GetFormat(url);
            var stream = await GetStream(url);
            return await LoadAsync(stream, format, settings);
        }

        /// <summary>
        /// Loads the input stream and parses it into an Open API document.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be cancelled.</param>
        /// <param name="format">The Open API format</param>
        /// <returns></returns>
        public static async Task<ReadResult> LoadAsync(Stream input, string format, OpenApiReaderSettings settings = null, CancellationToken cancellationToken = default)
        {
            Utils.CheckArgumentNull(format, nameof(format));
            settings ??= new OpenApiReaderSettings();

            MemoryStream bufferedStream;
            if (input is MemoryStream stream)
            {
                bufferedStream = stream;
            }
            else
            {
                // Buffer stream so that OpenApiTextReaderReader can process it synchronously
                // YamlDocument doesn't support async reading.
                bufferedStream = new MemoryStream();
                await input.CopyToAsync(bufferedStream, 81920, cancellationToken);
                bufferedStream.Position = 0;
            }

            using var reader = new StreamReader(bufferedStream, default, true, -1, settings.LeaveStreamOpen);
            return await LoadAsync(reader, format, settings, cancellationToken);
        }

        /// <summary>
        /// Loads the TextReader input and parses it into an Open API document.
        /// </summary>
        /// <param name="input">The TextReader input.</param>
        /// <param name="format">The Open API format</param>
        /// <param name="settings"> The OpenApi reader settings.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be cancelled.</param>
        /// <returns></returns>
        public static async Task<ReadResult> LoadAsync(TextReader input, string format, OpenApiReaderSettings settings = null, CancellationToken cancellationToken = default)
        {
            Utils.CheckArgumentNull(format, nameof(format));
            var reader = OpenApiReaderRegistry.GetReader(format);
            return await reader.ReadAsync(input, settings, cancellationToken);
        }

        /// <summary>
        /// Reads the input string and parses it into an Open API document.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="format">The Open API format</param>
        /// <param name="settings">The OpenApi reader settings.</param>
        /// <returns>An OpenAPI document instance.</returns>
        public static ReadResult Parse(string input,
                                       string format = null,
                                       OpenApiReaderSettings settings = null)
        {
            format ??= OpenApiConstants.Json;
            settings ??= new OpenApiReaderSettings();
            using var reader = new StringReader(input);
            return LoadAsync(reader, format, settings).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads the input string and parses it into a DOM of type T.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="version"></param>
        /// <param name="diagnostic">The diagnostic entity containing information from the reading process.</param>
        /// <param name="format">The Open API format</param>
        /// <param name="settings">The OpenApi reader settings.</param>
        /// <returns>A DOM of type T.</returns>
        public static T Parse<T>(string input,
                                 OpenApiSpecVersion version,
                                 out OpenApiDiagnostic diagnostic,
                                 string format = null,
                                 OpenApiReaderSettings settings = null) where T : IOpenApiElement
        {
            format ??= OpenApiConstants.Json;
            settings ??= new OpenApiReaderSettings();
            using var reader = new StringReader(input);
            return Load<T>(reader, version, out diagnostic, format, settings);
        }

        /// <summary>
        /// Reads the stream input and parses the fragment of an OpenAPI description into an Open API Element.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url">The path to the OpenAPI file</param>
        /// <param name="version">Version of the OpenAPI specification that the fragment conforms to.</param>
        /// <param name="diagnostic">Returns diagnostic object containing errors detected during parsing.</param>
        /// <param name="settings">The OpenApiReader settings.</param>
        /// <returns>Instance of newly created IOpenApiElement.</returns>
        /// <returns>The OpenAPI element.</returns>
        public static T Load<T>(string url, OpenApiSpecVersion version, out OpenApiDiagnostic diagnostic, OpenApiReaderSettings settings = null) where T : IOpenApiElement
        {
            var format = GetFormat(url);
            settings ??= new OpenApiReaderSettings();
            var stream = GetStream(url).GetAwaiter().GetResult();
            return Load<T>(stream, version, format, out diagnostic, settings);
        }

        /// <summary>
        /// Reads the stream input and parses the fragment of an OpenAPI description into an Open API Element.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">Stream containing OpenAPI description to parse.</param>
        /// <param name="version">Version of the OpenAPI specification that the fragment conforms to.</param>
        /// <param name="format"></param>
        /// <param name="diagnostic">Returns diagnostic object containing errors detected during parsing.</param>
        /// <param name="settings">The OpenApiReader settings.</param>
        /// <returns>Instance of newly created IOpenApiElement.</returns>
        /// <returns>The OpenAPI element.</returns>
        public static T Load<T>(Stream input, OpenApiSpecVersion version, string format, out OpenApiDiagnostic diagnostic, OpenApiReaderSettings settings = null) where T : IOpenApiElement
        {
            format ??= OpenApiConstants.Json;
            using var reader = new StreamReader(input);
            return Load<T>(reader, version, out diagnostic, format, settings);
        }

        /// <summary>
        /// Reads the TextReader input and parses the fragment of an OpenAPI description into an Open API Element.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">TextReader containing OpenAPI description to parse.</param>
        /// <param name="version">Version of the OpenAPI specification that the fragment conforms to.</param>
        /// <param name="format">The OpenAPI format.</param>
        /// <param name="diagnostic">Returns diagnostic object containing errors detected during parsing.</param>
        /// <param name="settings">The OpenApiReader settings.</param>
        /// <returns>Instance of newly created IOpenApiElement.</returns>
        /// <returns>The OpenAPI element.</returns>
        public static T Load<T>(TextReader input, OpenApiSpecVersion version, out OpenApiDiagnostic diagnostic, string format, OpenApiReaderSettings settings = null) where T : IOpenApiElement
        {
            format ??= OpenApiConstants.Json;
            return OpenApiReaderRegistry.GetReader(format).ReadFragment<T>(input, version, out diagnostic, settings);
        }


        private static string GetContentType(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                var mediaType = response.Content.Headers.ContentType.MediaType;
                return mediaType.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).First();
            }

            return null;
        }

        /// <summary>
        /// Infers the OpenAPI format from the input URL.
        /// </summary>
        /// <param name="url">The input URL.</param>
        /// <returns>The OpenAPI format.</returns>
        public static string GetFormat(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    // URL examples ---> https://example.com/path/to/file.json, https://example.com/path/to/file.yaml
                    var path = new Uri(url);
                    var urlSuffix = path.Segments[path.Segments.Length - 1].Split('.').LastOrDefault();

                    return !string.IsNullOrEmpty(urlSuffix) ? urlSuffix : GetContentType(url).Split('/').LastOrDefault();
                }
                else
                {
                    return Path.GetExtension(url).Split('.').LastOrDefault();
                }
            }
            return null;
        }

        private static async Task<Stream> GetStream(string url)
        {
            Stream stream;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    stream = await _httpClient.GetStreamAsync(new Uri(url));
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException($"Could not download the file at {url}", ex);
                }
            }
            else
            {
                try
                {
                    var fileInput = new FileInfo(url);
                    stream = fileInput.OpenRead();
                }
                catch (Exception ex) when (
                    ex is
                        FileNotFoundException or
                        PathTooLongException or
                        DirectoryNotFoundException or
                        IOException or
                        UnauthorizedAccessException or
                        SecurityException or
                        NotSupportedException)
                {
                    throw new InvalidOperationException($"Could not open the file at {url}", ex);
                }
            }

            return stream;
        }

    }
}
