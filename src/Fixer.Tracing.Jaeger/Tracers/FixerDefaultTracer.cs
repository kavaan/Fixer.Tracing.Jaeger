﻿using OpenTracing;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;

namespace Fixer.Tracing.Jaeger.Tracers
{
    internal sealed class ConveyDefaultTracer
    {
        public static ITracer Create()
            => new Tracer.Builder(Assembly.GetEntryAssembly().FullName)
                .WithReporter(new NoopReporter())
                .WithSampler(new ConstSampler(false))
                .Build();
    }
}
