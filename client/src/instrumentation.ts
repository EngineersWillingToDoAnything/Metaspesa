export async function register() {
  if (process.env.NEXT_RUNTIME !== 'nodejs') return;
  const otlpEndpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT?.trim();
  if (!otlpEndpoint) return;

  const { NodeSDK } = await import('@opentelemetry/sdk-node');
  const { getNodeAutoInstrumentations } =
    await import('@opentelemetry/auto-instrumentations-node');
  const { OTLPTraceExporter } =
    await import('@opentelemetry/exporter-trace-otlp-grpc');
  const { OTLPMetricExporter } =
    await import('@opentelemetry/exporter-metrics-otlp-grpc');
  const { OTLPLogExporter } =
    await import('@opentelemetry/exporter-logs-otlp-grpc');
  const { PeriodicExportingMetricReader } =
    await import('@opentelemetry/sdk-metrics');
  const { BatchLogRecordProcessor } = await import('@opentelemetry/sdk-logs');
  const { resourceFromAttributes } = await import('@opentelemetry/resources');
  const { ATTR_SERVICE_NAME } =
    await import('@opentelemetry/semantic-conventions');

  const sdk = new NodeSDK({
    instrumentations: [getNodeAutoInstrumentations()],
    logRecordProcessors: [
      new BatchLogRecordProcessor({
        exporter: new OTLPLogExporter({ url: otlpEndpoint }),
      }),
    ],
    metricReaders: [
      new PeriodicExportingMetricReader({
        exportIntervalMillis: Number(
          process.env.OTEL_METRIC_EXPORT_INTERVAL ?? '60000',
        ),
        exporter: new OTLPMetricExporter({ url: otlpEndpoint }),
      }),
    ],
    resource: resourceFromAttributes({
      [ATTR_SERVICE_NAME]: 'metaspesa_web_client',
    }),
    traceExporter: new OTLPTraceExporter({ url: otlpEndpoint }),
  });

  sdk.start();
}
