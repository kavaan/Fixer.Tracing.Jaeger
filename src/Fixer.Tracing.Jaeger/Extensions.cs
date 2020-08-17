using System;
using Fixer.Tracing.Jaeger.Builders;
using Fixer.Tracing.Jaeger.Middlewares;
using Fixer.Tracing.Jaeger.Tracers;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;

namespace Fixer.Tracing.Jaeger
{
    public static class Extensions
    {
        private static bool _initialized;
        private const string SectionName = "jaeger";
        private const string RegistryName = "tracing.jaeger";

        public static IFixerBuilder AddJaeger(this IFixerBuilder builder, string sectionName = SectionName)
        {
            var options = builder.GetOptions<JaegerOptions>(sectionName);
            return builder.AddJaeger(options);
        }

        public static IFixerBuilder AddJaeger(this IFixerBuilder builder,
            Func<IJaegerOptionsBuilder, IJaegerOptionsBuilder> buildOptions)
        {
            var options = buildOptions(new JaegerOptionsBuilder()).Build();
            return builder.AddJaeger(options);
        }

        public static IFixerBuilder AddJaeger(this IFixerBuilder builder, JaegerOptions options)
        {
            builder.Services.AddSingleton(options);
            if (!options.Enabled)
            {
                var defaultTracer = ConveyDefaultTracer.Create();
                builder.Services.AddSingleton(defaultTracer);
                return builder;
            }

            if (!builder.TryRegister(RegistryName) || _initialized)
            {
                return builder;
            }

            _initialized = true;
            builder.Services.AddSingleton<ITracer>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                var reporter = new RemoteReporter.Builder()
                    .WithSender(new UdpSender(options.UdpHost, options.UdpPort, options.MaxPacketSize))
                    .WithLoggerFactory(loggerFactory)
                    .Build();

                var sampler = GetSampler(options);

                var tracer = new Tracer.Builder(options.ServiceName)
                    .WithLoggerFactory(loggerFactory)
                    .WithReporter(reporter)
                    .WithSampler(sampler)
                    .Build();

                GlobalTracer.Register(tracer);

                return tracer;
            });

            return builder;
        }

        public static IApplicationBuilder UseJaeger(this IApplicationBuilder app)
        {
            JaegerOptions options;
            using (var scope = app.ApplicationServices.CreateScope())
            {
                options = scope.ServiceProvider.GetService<JaegerOptions>();
            }

            return options.Enabled ? app.UseMiddleware<JaegerHttpMiddleware>() : app;
        }

        private static ISampler GetSampler(JaegerOptions options)
        {
            switch (options.Sampler)
            {
                case "const": return new ConstSampler(true);
                case "rate": return new RateLimitingSampler(options.MaxTracesPerSecond);
                case "probabilistic": return new ProbabilisticSampler(options.SamplingRate);
                default: return new ConstSampler(true);
            }
        }
    }
}
