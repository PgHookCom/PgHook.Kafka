# PgHook.Kafka
Containerized app that uses PostgreSQL logical replication to send row changes to a Kafka topic

## Example Usage

```bash
docker run --network=host --rm \
  -e PGH_POSTGRES_CONN="Host=localhost;Username=postgres;Password=xxxxxxxxx;Database=test_db;ApplicationName=PgHook" \
  -e PGH_PUBLICATION_NAMES="test_publication" \
  -e PGH_KAFKA_SERVERS="localhost:9092" \
  -e PGH_KAFKA_TOPIC="test_topic" \
  pghook/pghook-kafka
```

## Custom partition keys

By default, the table's PK fields are used for partition keys. User can override it by providing `PGH_KAFKA_PARTITION_KEY_FIELDS_X` environment
variables (X is a number starting from 1). For example:

```bash
docker run --network=host --rm \
  -e PGH_POSTGRES_CONN="Host=localhost;Username=postgres;Password=xxxxxxxx;Database=test_db;ApplicationName=PgHook" \
  -e PGH_PUBLICATION_NAMES="test_publication" \
  -e PGH_KAFKA_SERVERS="localhost:9092" \
  -e PGH_KAFKA_TOPIC="test_topic" \
  -e PGH_KAFKA_PARTITION_KEY_FIELDS_1="public.table_a|last_name,first_name" \
  -e PGH_KAFKA_PARTITION_KEY_FIELDS_2="public.table_b|postalcode,address" \
  pghook/pghook-kafka
```