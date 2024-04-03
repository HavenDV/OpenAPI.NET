﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using Json.Schema;
using Microsoft.OpenApi.Any;

namespace Microsoft.OpenApi.Reader.ParseNodes
{
    internal class AnyFieldMapParameter<T>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AnyFieldMapParameter(
            Func<T, OpenApiAny> propertyGetter,
            Action<T, OpenApiAny> propertySetter,
            Func<T, JsonSchema> SchemaGetter = null)
        {
            this.PropertyGetter = propertyGetter;
            this.PropertySetter = propertySetter;
            this.SchemaGetter = SchemaGetter;
        }

        /// <summary>
        /// Function to retrieve the value of the property.
        /// </summary>
        public Func<T, OpenApiAny> PropertyGetter { get; }

        /// <summary>
        /// Function to set the value of the property.
        /// </summary>
        public Action<T, OpenApiAny> PropertySetter { get; }

        /// <summary>
        /// Function to get the schema to apply to the property.
        /// </summary>
        public Func<T, JsonSchema> SchemaGetter { get; }
    }
}
