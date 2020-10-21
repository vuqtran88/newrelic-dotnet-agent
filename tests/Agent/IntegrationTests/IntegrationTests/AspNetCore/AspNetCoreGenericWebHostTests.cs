// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    [NetCoreTest]
    public class AspNetCoreGenericWebHostTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCore3FeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCore3FeaturesFixture _fixture;

        private const int ExpectedTransactionCount = 2;

        public AspNetCoreGenericWebHostTests(RemoteServiceFixtures.AspNetCore3FeaturesFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.ThrowException();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ErrorTraceDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assert.NotNull(metrics);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_indexMetrics, metrics)
            );

            var expectedTransactionTraceSegments = new List<string>
            {
                @"Middleware Pipeline",
                @"DotNet/HomeController/Index"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/MVC/Home/Index");

            Assert.NotNull(transactionSample);
            Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample);

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", "System.Exception" },
                { "error.message", "ExceptionMessage" },
            };

            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            NrAssert.Multiple(
                () => Assert.True(errorTraces.Any(), "No error trace found."),
                () => Assert.True(errorTraces.Count == 1, $"Expected 1 errors traces but found {errorTraces.Count}"),
                () => Assert.Equal("WebTransaction/MVC/Home/ThrowException", errorTraces[0].Path),
                () => Assert.Equal("System.Exception", errorTraces[0].ExceptionClassName),
                () => Assert.Equal("ExceptionMessage", errorTraces[0].Message),
                () => Assert.NotEmpty(errorTraces[0].Attributes.StackTrace),
                () => Assert.Single(errorEvents),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvents[0].Events[0])
            );
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/OS/Linux", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Apdex"},
            new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
            new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = ExpectedTransactionCount },

            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", callCount = ExpectedTransactionCount }
        };

        private readonly List<Assertions.ExpectedMetric> _indexMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/Home/Index"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/Home/Index", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 }
        };

        private readonly List<Assertions.ExpectedMetric> _throwMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/Home/ThrowException"},
            new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/ThrowException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/Home/ThrowException", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/ThrowException", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/ThrowException", metricScope = @"WebTransaction/MVC/Home/ThrowException", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Home/ThrowException", callCount = 1 }
        };
    }
}
