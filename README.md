# On-Call Survival Lab for Backend Engineers

This lab is designed to help you practice **incident investigation**, not infrastructure theory.

It gives you:
- Grafana for dashboards and exploration
- Prometheus for metrics
- Loki + Promtail for logs
- Tempo for traces
- OpenTelemetry Collector for telemetry routing
- Two .NET 8 APIs (`order-api` and `inventory-api`) you can break on purpose

This is a **local training lab** for becoming comfortable with on-call work.

## Architecture

`order-api -> inventory-api`

Both services emit:
- metrics to the OpenTelemetry Collector, which exposes them for Prometheus
- traces to Tempo through the Collector
- JSON logs to files, which Promtail ships to Loki

## Prerequisites

- Docker Desktop
- Docker Compose

## Start the lab

```bash
docker compose up --build
```

Open:
- Grafana: http://localhost:3000
- Prometheus: http://localhost:9090
- Order API: http://localhost:8080/health
- Inventory API: http://localhost:8081/health

Grafana login:
- username: `admin`
- password: `admin`

## Generate normal traffic

Run this a few times:

```bash
for i in {1..30}; do curl -s http://localhost:8080/orders/$i > /dev/null; done
```

## Inject incidents

### 1. Slow downstream service

```bash
curl -X POST http://localhost:8081/chaos \
  -H 'Content-Type: application/json' \
  -d '{"mode":"off","failRate":0.0,"delayMs":2500}'
```

What you should see:
- `order-api` latency increases
- traces show time spent inside `inventory-api`
- logs show delay warnings

### 2. Downstream failures

```bash
curl -X POST http://localhost:8081/chaos \
  -H 'Content-Type: application/json' \
  -d '{"mode":"fail","failRate":0.0,"delayMs":0}'
```

What you should see:
- `order-api` returns 502s
- `inventory-api` shows 500s
- 5xx graphs spike
- logs in both services show the dependency failure chain
- Tempo shows the failing span path

### 3. Flaky service

```bash
curl -X POST http://localhost:8081/chaos \
  -H 'Content-Type: application/json' \
  -d '{"mode":"off","failRate":0.3,"delayMs":0}'
```

What you should see:
- intermittent 5xx
- harder diagnosis
- good practice for noisy incidents

### 4. Reset chaos

```bash
curl -X POST http://localhost:8081/chaos \
  -H 'Content-Type: application/json' \
  -d '{"mode":"off","failRate":0.0,"delayMs":0}'
```

## Your investigation playbook

When an alert fires, follow this order:

1. **Scope**
   - Which service is failing?
   - Is it all traffic or one endpoint?
   - Did it start suddenly or gradually?

2. **Metrics**
   - Request rate
   - Error rate
   - p95 latency
   - Exceptions

3. **Logs**
   - Search by service name
   - Search for `error`, `fail`, `timeout`, `exception`
   - Compare `order-api` logs with `inventory-api` logs

4. **Traces**
   - Which span is slow?
   - Which service returns the first error?
   - Is the problem local or downstream?

5. **Mitigation**
   - Reduce traffic
   - Roll back
   - Disable a feature
   - Restart a bad dependency
   - Escalate if data integrity is at risk

## Suggested drills

### Drill A: Latency spike
1. Inject 2500 ms delay in `inventory-api`
2. Generate traffic
3. Find the issue from metrics first
4. Confirm with traces
5. Validate in logs
6. Write a short incident summary

### Drill B: Error-rate spike
1. Put `inventory-api` in fail mode
2. Generate traffic
3. Find which service is the source
4. Distinguish symptom service from root-cause service

### Drill C: Intermittent failures
1. Set `failRate` to `0.3`
2. Generate traffic for 2 minutes
3. Try to explain why the graph looks noisy
4. Decide whether to page immediately or continue investigating

## What to practice as the on-call engineer

For every drill, answer these questions:
- What alerted first?
- What is user impact?
- What is the likely blast radius?
- What changed recently?
- What is the fastest safe mitigation?
- What evidence proves the root cause?

## Next level improvements

After you are comfortable, add:
- PostgreSQL and database-related incidents
- Redis and cache outages
- message queues and backlog incidents
- alerting rules in Prometheus or Grafana
- SLO dashboards
- synthetic checks

## Troubleshooting

### No data in Grafana
Generate traffic first:

```bash
for i in {1..100}; do curl -s http://localhost:8080/orders/$i > /dev/null; done
```

### No logs in Loki
Check:

```bash
docker compose logs promtail
docker compose logs loki
ls -la ./logs
```

### No traces in Tempo
Check:

```bash
docker compose logs otel-collector
docker compose logs tempo
```

## Important mindset shift

The goal is not to become a Grafana installer.

The goal is to become the engineer who can say:

> "The alert is real. The issue started in `inventory-api`. It is causing elevated 502s in `order-api`. User impact is limited to the order endpoint. The immediate mitigation is to disable chaos or roll back the bad change."

That is on-call thinking.

# OnCall Lab

Local observability practice lab for backend on-call training.

## Stack
- Grafana
- Prometheus
- Loki + Promtail
- Tempo
- OpenTelemetry Collector
- `order-api` (.NET 8)
- `inventory-api` (.NET 8)

## Start
```bash
docker compose up --build
```

Grafana: `http://localhost:3000`  
Prometheus: `http://localhost:9090`  
Order API: `http://localhost:8080/health`  
Inventory API: `http://localhost:8081/health`

Grafana login:
- user: `admin`
- password: `admin`

## Generate traffic
Steady 5-minute traffic:
```bash
./traffic/steady-traffic.sh
```

Burst traffic:
```bash
./traffic/generate-traffic.sh
```

Infinite traffic:
```bash
./traffic/infinite-traffic.sh
```

k6 load test:
```bash
brew install k6
./traffic/run-k6.sh
```

## Trigger incidents
Slow downstream:
```bash
./incidents/slow-dependency.sh
```

Hard downstream failure:
```bash
./incidents/dependency-failure.sh
```

Flaky downstream failure:
```bash
./incidents/flaky-dependency.sh
```

Order API failure:
```bash
./incidents/order-api-failure.sh
```

Reset chaos:
```bash
./incidents/reset-chaos.sh
```

## Suggested drill flow
1. Start the stack.
2. Run `./traffic/steady-traffic.sh`.
3. Open Grafana dashboard `OnCall Overview`.
4. Trigger `./incidents/slow-dependency.sh`.
5. Investigate metrics, logs, and traces.
6. Reset with `./incidents/reset-chaos.sh`.
7. Trigger `./incidents/dependency-failure.sh` and repeat.
