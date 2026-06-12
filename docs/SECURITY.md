# Security documentation (Phase 4)

This document describes how the system secures sensor → server communication
and analyzes the risks of running it over a real network.

## Message flow

```
SensorSimulator                                IngestionService
---------------                                ----------------
build SensorMessage
  (id, value, timestamp, messageId, ...)
serialize to JSON
encrypt with fresh AES-256-GCM key   ────┐
wrap AES key with server RSA pub key     │     1. rate limiter (10 req/s per X-Sensor-Id)
sign envelope with sensor ECDSA key      │     2. verify ECDSA signature (sensor pub key)
POST SecureEnvelope ─────────────────────┴──►  3. unwrap AES key (server RSA priv key)
  + X-Sensor-Id header                         4. decrypt + authenticate (AES-GCM)
                                               5. validate timestamp window (replay)
                                               6. validate MessageId monotonicity (replay)
                                               7. existing checks (active, blocked, range)
                                               8. persist reading / alarm
```

## Cryptographic design

Every message a sensor sends is **encrypted** (confidentiality) and
**digitally signed** (sender authenticity + integrity), per the project
requirement "AES i RSA/ECDSA".

| Aspect | Choice | Rationale |
|---|---|---|
| Payload encryption | AES-256-GCM (fresh key per message, 12-byte nonce, 16-byte tag) | Authenticated encryption from the .NET BCL; tampering is detected by the GCM tag; no CBC padding pitfalls. |
| Key transport | RSA-2048-OAEP-SHA256 wrapping of the per-message AES key | Textbook AES+RSA hybrid: only the server (private-key holder) can unwrap; no shared secret needs distributing; a leaked AES key exposes a single message. |
| Signature | ECDSA P-256 + SHA-256, one key pair per sensor | Identifies the individual sensor; small keys and signatures; one BCL class. |
| Construction | Encrypt-then-sign; the signature covers every envelope field | The server verifies the signature *first* and drops forged traffic before doing any RSA/AES work, which also helps DoS resilience. |

The reusable implementation lives in the `SensorMonitoring.Security` library:
`SensorMessageProtector` (client side) and `SecureEnvelopeOpener` (server
side) share `EnvelopeSigningPayload` as the single definition of the signed
bytes (each field length-prefixed, so field boundaries are unambiguous).

### Wire format

```json
{
  "sensorId":     "SENSOR-001",          // plaintext (see below)
  "encryptedKey": "<base64 RSA-wrapped AES key>",
  "nonce":        "<base64 12-byte GCM nonce>",
  "ciphertext":   "<base64 encrypted SensorMessage JSON>",
  "tag":          "<base64 16-byte GCM tag>",
  "signature":    "<base64 ECDSA signature>"
}
```

**What stays plaintext, and why that is acceptable:** the sensor ID appears
in the envelope and in the `X-Sensor-Id` header. It is needed *before*
decryption (to pick the right public key, and to partition the rate limiter
before the body is parsed), and it is not a secret — it identifies, but does
not authenticate. Authentication comes only from the ECDSA signature, and
the server rejects requests whose header and (signed) envelope IDs disagree,
so the header cannot be used to impersonate or shift blame to another
sensor. The measured temperature, timestamps and alarm data are all inside
the ciphertext.

### Key management

`dotnet run --project src/SensorSimulator -- --keygen` generates all key
material into a git-ignored `keys/` folder:

```
keys/
  server/                    # stays on the server machine
    server_rsa_private.pem
    sensors/SENSOR-00X.public.pem
  client/                    # copied to the sensor machine
    server_rsa_public.pem
    sensors/SENSOR-00X.private.pem
```

Keys are deliberately **not** committed to the repository. For the defense
demo they are provisioned by copying `keys/client/` to the client machine —
an out-of-band channel, the same trust model as installing a certificate. In
production this would be replaced by per-device provisioning at manufacture
or registration, secret storage (HSM / vault), and key rotation.

## Threat analysis for real-network deployment

The services bind to all interfaces (`http://+:8080` in Docker, published as
port 5001), so the ingestion endpoint is reachable from the LAN — required
for the two-machine defense setup. Communication is plain HTTP by design
(see TLS note below); message-level crypto provides the protection.

| Threat | Mitigation | Residual risk |
|---|---|---|
| **Sniffing** (reading traffic on the wire) | Payload is AES-256-GCM encrypted; an observer sees only base64 ciphertext. | Traffic analysis: sensor IDs, message sizes and frequency are visible. TLS would hide these too. |
| **MITM / tampering** (modifying messages in flight) | Any change to ciphertext, nonce, tag or sensor ID invalidates the GCM tag and/or the ECDSA signature → rejected with 401/400. An attacker cannot forge messages without a sensor's private key. | An active MITM can still drop or delay messages (denial of delivery). The fault-tolerance pool (Phase 2) detects silent sensors after 10 s. |
| **Replay** (re-sending a captured valid message) | Each message carries a timestamp (rejected outside a ±30 s window) and a strictly increasing `MessageId` tracked per sensor in the DB column `Sensors.LastMessageId` → rejected with 409. DB-backed state means replays are still rejected after a server restart. | Requires reasonably synchronized clocks between machines (tolerance is configurable via `Security:TimestampToleranceSeconds`). |
| **Sender spoofing** (pretending to be a sensor) | Per-sensor ECDSA private keys; the server verifies against the registered public key. | Theft of a sensor's private-key file compromises that one sensor identity. Production: secure element / vault storage. |
| **DoS by a malicious sensor** | Fixed-window rate limit of 10 requests/s per sensor ID (HTTP 429), enforced before the body is read; violators are additionally blocked for 30 s via the existing sensor-pool mechanism, and a standby sensor is promoted. | An attacker spraying many *different* sensor IDs falls back to per-IP partitioning; a distributed flood must be absorbed upstream (reverse proxy / ingress rate limiting, firewall). |
| **Key compromise** | Keys live outside the repo; the server only holds public sensor keys, so a server breach does not let an attacker sign as a sensor. | The server's RSA private key decrypts all captured past traffic (no forward secrecy). Production: TLS with ephemeral key exchange, key rotation. |
| **Unauthenticated admin endpoint** | — | `POST /api/sensors/{id}/block` (the Phase 2 manual test endpoint) is intentionally unauthenticated; on a real network it should be protected (API key / network policy). Known, accepted for the demo. |

## Rate-limiter library choice

The assignment suggests `AspNetCoreRateLimit`. That community package is no
longer maintained and predates .NET 8. We use the framework's first-party
successor, **`Microsoft.AspNetCore.RateLimiting`** (built into ASP.NET Core
8), configured with the same fixed-window algorithm the suggestion implies:
10 permits per 1-second window, partitioned by sensor ID, no queueing,
HTTP 429 on rejection. Same semantics, supported implementation.


## Two-machine deployment notes

- The server machine runs `docker compose up`; port **5001** must be allowed
  through its firewall:
  `New-NetFirewallRule -DisplayName "SNUS ingestion" -Direction Inbound -Protocol TCP -LocalPort 5001 -Action Allow`
- Find the server's LAN IP with `ipconfig` (IPv4 address).
- Copy `keys/client/` to the client machine; point the simulator at the
  server with the `IngestionBaseUrl` environment variable.
- Clock synchronization matters: if the two machines drift more than the
  timestamp tolerance (30 s), valid messages are rejected as replays. Both
  machines using NTP (Windows default) is sufficient.

