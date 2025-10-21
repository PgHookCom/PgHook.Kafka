# PgHook.Kafka
Containerized app that uses PostgreSQL logical replication to send row changes to a Kafka topic

## Quick start

1) To enable logical replication, add the following setting in your `postgresql.conf`:

```
wal_level = logical

# If needed increase the number of WAL senders, replication slots.
# The default is 10 for both.
max_wal_senders = 10
max_replication_slots = 10
```

Other necessary settings usually have appropriate default values for a basic setup.

> **Note:** PostgreSQL must be **restarted** after modifying this setting.

2) Ensure PostgreSQL has a publication for the tables you want to watch:
```sql
CREATE PUBLICATION mypub FOR TABLE table1, table2;
```

3) Run PgHook.Kafka with the minimum required environment:
```bash
docker run --network=host --rm \
  -e PGH_POSTGRES_CONN="Host=localhost;Username=postgres;Password=xxxxxxxxx;Database=test_db;ApplicationName=PgHook" \
  -e PGH_PUBLICATION_NAMES="test_publication" \
  -e PGH_KAFKA_SERVERS="localhost:9092" \
  -e PGH_KAFKA_TOPIC="test_topic" \
  pghook/kafka
```

That’s it. As rows change, PgHook will send messages to Kafka.

> Want a stable replication position across restarts? Create the replication slot in PostgreSQL:  
> `SELECT * FROM pg_create_logical_replication_slot('my_slot', 'pgoutput');`  
> 
> Add environment variables:  
> `-e PGH_USE_PERMANENT_SLOT=true \`  
> `-e PGH_REPLICATION_SLOT=myslot \`  
> 
> ⚠️ **Important:** Permanent replication slots persist across crashes and know nothing about the state of their consumer(s). 
> They will prevent removal of required resources when there is no connection using them. 
> This consumes storage because neither required WAL nor required rows from the system catalogs can be removed by VACUUM as long as they are required by a replication slot. 
> **In extreme cases, this could cause the database to shut down to prevent transaction ID wraparound.**  
> 
> **So, if a slot is no longer required, it should be dropped.** To drop a replication slot, use:  
> `SELECT * FROM pg_drop_replication_slot('my_slot');`

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
  pghook/kafka
```
