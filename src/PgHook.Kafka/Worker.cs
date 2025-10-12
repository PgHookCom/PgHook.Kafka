using Microsoft.Extensions.Options;
using PgOutput2Json;

namespace PgHook.Kafka
{
    public class Worker(IConfiguration cfg, ILoggerFactory loggerFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var kafkaBootstrapServers = cfg.GetValue<string>("PGH_KAFKA_SERVERS") ?? "";
            if (string.IsNullOrWhiteSpace(kafkaBootstrapServers))
            {
                throw new Exception("PGH_KAFKA_SERVERS is not set");
            }

            var kafkaTopic = cfg.GetValue<string>("PGH_KAFKA_TOPIC") ?? "";
            if (string.IsNullOrWhiteSpace(kafkaTopic))
            {
                throw new Exception("PGH_KAFKA_TOPIC is not set");
            }

            var partitionKeyFields = new Dictionary<string, List<string>>();

            for (var i = 1; true; i++)
            {
                var keyFieldsString = cfg.GetValue<string>($"PGH_KAFKA_PARTITION_KEY_FIELDS_{i}");
                if (string.IsNullOrWhiteSpace(keyFieldsString)) break;

                AddPartitionKeyFields(partitionKeyFields, keyFieldsString);
            }

            var kafkaWriteHeaders = cfg.GetValue<bool>("PGH_KAFKA_WRITE_HEADERS");

            var connectionString = cfg.GetValue<string>("PGH_POSTGRES_CONN") ?? "";
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new Exception("PGH_POSTGRES_CONN is not set");
            }

            var publicationNamesCfg = cfg.GetValue<string>("PGH_PUBLICATION_NAMES") ?? "";
            if (string.IsNullOrWhiteSpace(publicationNamesCfg))
            {
                throw new Exception("PGH_PUBLICATION_NAMES is not set");
            }

            string[] publicationNames = [.. publicationNamesCfg.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())];

            var usePermanentSlot = cfg.GetValue<bool>("PGH_USE_PERMANENT_SLOT");

            var replicationSlot = cfg.GetValue<string>("PGH_REPLICATION_SLOT") ?? "";
            if (string.IsNullOrWhiteSpace(replicationSlot))
            {
                if (usePermanentSlot)
                {
                    throw new Exception("PGH_REPLICATION_SLOT is not set");
                }
                else
                {
                    replicationSlot = $"pghook_{Guid.NewGuid().ToString().Replace("-", "")}";
                }
            }

            var batchSize = cfg.GetValue<int>("PGH_BATCH_SIZE");
            if (batchSize < 1)
            {
                batchSize = 100;
            }

            var useCompactJson = cfg.GetValue<bool>("PGH_JSON_COMPACT");

            using var pgOutput2Json = PgOutput2JsonBuilder.Create()
                .WithLoggerFactory(loggerFactory)
                .WithPgConnectionString(connectionString)
                .WithPgPublications(publicationNames)
                .WithPgReplicationSlot(replicationSlot, useTemporarySlot: !usePermanentSlot)
                .WithBatchSize(batchSize)
                .WithJsonOptions(jsonOptions =>
                {
                    jsonOptions.WriteTableNames = true;
                    jsonOptions.WriteTimestamps = true;
                    jsonOptions.TimestampFormat = TimestampFormat.UnixTimeMilliseconds;
                    jsonOptions.WriteMode = useCompactJson ? JsonWriteMode.Compact : JsonWriteMode.Default;
                })
                .UseKafka(options =>
                {
                    options.ProducerConfig.BootstrapServers = kafkaBootstrapServers; // "localhost:9092";
                    options.Topic = kafkaTopic; // "test_topic";
                    options.WriteHeaders = kafkaWriteHeaders;
                    options.PartitionKeyFields = partitionKeyFields;
                })
                .Build();

            await pgOutput2Json.StartAsync(stoppingToken);
        }

        private static void AddPartitionKeyFields(Dictionary<string, List<string>> keyFields, string keyFieldsString)
        {
            var parts = keyFieldsString.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2) throw new Exception("Invalid key fields string: " + keyFieldsString);

            var tableName = parts[0];

            List<string> fields = [.. parts[1]
                .Trim()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            
            keyFields.Add(tableName, fields);
        }
    }
}
