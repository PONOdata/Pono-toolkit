"""
Pono iMessage bridge - Windows server (Phase 1, hardened).

Listens for iMessage payloads pushed by an iOS Shortcuts automation on the
user's iPhone. The iPhone is the source of truth for iMessage; this process
only receives, stores, and renders them on the Windows side. Phase 2 will
layer outbound reply via a Swift companion app + Bonjour.

Security notes (post-Spottswoode grading, 2026-04-18):
  - Shared secret is accepted ONLY via the X-Pono-Key header. Never via URL
    query string (would leak to access logs + browser history).
  - Uvicorn access log uses a custom filter that drops the header value and
    redacts any key= substrings encountered in legacy paths.
  - Body-size cap enforced by middleware before Pydantic validation.
  - Browser UI stores the key in sessionStorage (cleared on tab close), not
    localStorage. CSP header blocks cross-origin script injection.
  - The at-rest shared_secret.txt file is ACL-hardened by setup.ps1.

See docs/architecture.md for topology and docs/limitations.md for iOS caveats.
"""

from __future__ import annotations

import hashlib
import hmac
import logging
import os
import re
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterator, Optional

from fastapi import Depends, FastAPI, Header, HTTPException, Query, Request
from fastapi.responses import HTMLResponse, JSONResponse
from pydantic import BaseModel, Field
from starlette.middleware.base import BaseHTTPMiddleware

# ---- config ---------------------------------------------------------------

SHARED_SECRET = os.environ.get("PONO_IMESSAGE_KEY", "").strip()
if not SHARED_SECRET:
    raise SystemExit(
        "PONO_IMESSAGE_KEY environment variable is required. Run setup.ps1 to generate one."
    )

DB_DIR = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "pono-imessage"
DB_DIR.mkdir(parents=True, exist_ok=True)
DB_PATH = DB_DIR / "messages.db"

HOST = os.environ.get("PONO_IMESSAGE_HOST", "127.0.0.1")
PORT = int(os.environ.get("PONO_IMESSAGE_PORT", "8765"))

# Body-size cap. 256 KB is way more than any legitimate iMessage payload.
MAX_BODY_BYTES = int(os.environ.get("PONO_IMESSAGE_MAX_BODY", 256 * 1024))

# ---- logging with secret redaction ---------------------------------------

# Pattern that matches anything that looks like our secret or a key= param.
_SECRET_PATTERNS = [
    re.compile(re.escape(SHARED_SECRET)),
    re.compile(r"[?&]key=[^\s&'\"]+", re.IGNORECASE),
    re.compile(r"X-Pono-Key:\s*[^\s]+", re.IGNORECASE),
]


class _SecretRedactFilter(logging.Filter):
    """Scrub any occurrence of the shared secret or a key= query param from log records.
    Defense in depth in case a future change lets a secret slip into a log line."""

    def filter(self, record: logging.LogRecord) -> bool:
        if isinstance(record.msg, str):
            msg = record.msg
            for p in _SECRET_PATTERNS:
                msg = p.sub("[REDACTED]", msg)
            record.msg = msg
        if record.args:
            try:
                safe_args = []
                for a in record.args if isinstance(record.args, tuple) else (record.args,):
                    if isinstance(a, str):
                        for p in _SECRET_PATTERNS:
                            a = p.sub("[REDACTED]", a)
                    safe_args.append(a)
                record.args = tuple(safe_args) if isinstance(record.args, tuple) else safe_args[0]
            except Exception:
                pass
        return True


_redact = _SecretRedactFilter()
for name in ("uvicorn", "uvicorn.access", "uvicorn.error", "fastapi", "pono-imessage"):
    logging.getLogger(name).addFilter(_redact)

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
LOG = logging.getLogger("pono-imessage")
LOG.addFilter(_redact)

# ---- db -------------------------------------------------------------------


def _init_db() -> None:
    with sqlite3.connect(DB_PATH) as conn:
        conn.executescript(
            """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS messages (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                received_at       TEXT    NOT NULL,
                ios_timestamp     TEXT,
                sender            TEXT    NOT NULL,
                thread_id         TEXT    NOT NULL,
                body              TEXT    NOT NULL,
                direction         TEXT    NOT NULL CHECK (direction IN ('in','out')),
                source            TEXT    NOT NULL DEFAULT 'ios-shortcut',
                is_group          INTEGER NOT NULL DEFAULT 0,
                group_title       TEXT,
                raw_payload_sha256 TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_msg_thread ON messages(thread_id, received_at DESC);
            CREATE INDEX IF NOT EXISTS idx_msg_time   ON messages(received_at DESC);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_msg_dedup ON messages(raw_payload_sha256);
            """
        )


@contextmanager
def _db() -> Iterator[sqlite3.Connection]:
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    try:
        yield conn
        conn.commit()
    finally:
        conn.close()


_init_db()


# ---- models ---------------------------------------------------------------


class InboundMessage(BaseModel):
    sender: str = Field(..., min_length=1, max_length=512)
    body: str = Field("", max_length=100_000)
    ios_timestamp: Optional[str] = Field(None, max_length=64)
    thread_id: Optional[str] = Field(None, max_length=256)
    is_group: bool = False
    group_title: Optional[str] = Field(None, max_length=512)


class StoredMessage(BaseModel):
    id: int
    received_at: str
    ios_timestamp: Optional[str]
    sender: str
    thread_id: str
    body: str
    direction: str
    source: str
    is_group: bool
    group_title: Optional[str]


# ---- helpers --------------------------------------------------------------


_PHONE_RE = re.compile(r"[^\d+]")


def _normalize_thread_key(raw: str) -> str:
    """Canonicalize a sender into a stable thread key.
    Phone numbers: strip non-digits except leading +. Assume NANP +1 if 10 digits.
    Emails: lowercase.
    Anything else: lowercase trim."""
    s = raw.strip()
    if "@" in s:
        return s.lower()
    digits = _PHONE_RE.sub("", s)
    if digits.startswith("+"):
        return digits
    if len(digits) == 10:
        return "+1" + digits
    if len(digits) == 11 and digits.startswith("1"):
        return "+" + digits
    return digits or s.lower()


# ---- auth -----------------------------------------------------------------


def _require_key(x_pono_key: Optional[str] = Header(default=None)) -> None:
    if x_pono_key is None or not hmac.compare_digest(x_pono_key, SHARED_SECRET):
        raise HTTPException(status_code=401, detail="bad or missing X-Pono-Key")


# ---- middleware -----------------------------------------------------------


class BodySizeLimit(BaseHTTPMiddleware):
    """Reject requests whose declared Content-Length exceeds the cap before we read them.
    Protects against memory exhaustion via oversized POST bodies."""

    async def dispatch(self, request: Request, call_next):
        cl = request.headers.get("content-length")
        if cl is not None:
            try:
                if int(cl) > MAX_BODY_BYTES:
                    return JSONResponse(
                        status_code=413,
                        content={"detail": f"payload too large (limit {MAX_BODY_BYTES} bytes)"},
                    )
            except ValueError:
                return JSONResponse(status_code=400, content={"detail": "invalid content-length"})
        return await call_next(request)


class SecurityHeaders(BaseHTTPMiddleware):
    """Set a strict CSP + related headers on every response.
    CSP 'default-src self' blocks any cross-origin script exfiltration path."""

    async def dispatch(self, request: Request, call_next):
        resp = await call_next(request)
        resp.headers.setdefault(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'",
        )
        resp.headers.setdefault("X-Content-Type-Options", "nosniff")
        resp.headers.setdefault("X-Frame-Options", "DENY")
        resp.headers.setdefault("Referrer-Policy", "no-referrer")
        resp.headers.setdefault("Cache-Control", "no-store")
        return resp


class RejectQueryStringSecret(BaseHTTPMiddleware):
    """Refuse any request that carries ?key= in the URL. The UI must pass the
    secret via X-Pono-Key header only. This defends against the easy mistake of
    a user bookmarking or sharing a URL that contains the shared secret."""

    async def dispatch(self, request: Request, call_next):
        if "key" in request.query_params:
            return JSONResponse(
                status_code=400,
                content={
                    "detail": "secret in URL rejected. Send as X-Pono-Key header only."
                },
            )
        return await call_next(request)


# ---- app ------------------------------------------------------------------

app = FastAPI(
    title="Pono iMessage Bridge",
    version="0.1.1",
    description="Phase 1: receive iMessages from iOS Shortcuts, store, render.",
)

# Order matters: query-string rejection first, then size limit, then security headers.
app.add_middleware(RejectQueryStringSecret)
app.add_middleware(BodySizeLimit)
app.add_middleware(SecurityHeaders)


@app.get("/healthz")
def healthz() -> dict:
    return {"ok": True, "db": str(DB_PATH), "time": datetime.now(timezone.utc).isoformat()}


@app.post("/inbound", status_code=201)
def inbound(msg: InboundMessage, _=Depends(_require_key)) -> dict:
    thread_id = _normalize_thread_key(msg.thread_id or msg.sender)
    received_at = datetime.now(timezone.utc).isoformat()

    payload_key = f"{msg.sender}|{msg.ios_timestamp or ''}|{msg.body}".encode("utf-8")
    sha = hashlib.sha256(payload_key).hexdigest()

    with _db() as conn:
        try:
            cur = conn.execute(
                """
                INSERT INTO messages
                    (received_at, ios_timestamp, sender, thread_id, body, direction,
                     source, is_group, group_title, raw_payload_sha256)
                VALUES (?, ?, ?, ?, ?, 'in', 'ios-shortcut', ?, ?, ?)
                """,
                (
                    received_at,
                    msg.ios_timestamp,
                    msg.sender,
                    thread_id,
                    msg.body,
                    1 if msg.is_group else 0,
                    msg.group_title,
                    sha,
                ),
            )
            msg_id = cur.lastrowid
            LOG.info("stored id=%s thread=%s bytes=%d", msg_id, thread_id, len(msg.body))
            return {"id": msg_id, "thread_id": thread_id, "stored_at": received_at, "dedup": False}
        except sqlite3.IntegrityError:
            row = conn.execute(
                "SELECT id, received_at FROM messages WHERE raw_payload_sha256 = ?",
                (sha,),
            ).fetchone()
            LOG.info("dedup id=%s", row["id"])
            return {"id": row["id"], "thread_id": thread_id, "stored_at": row["received_at"], "dedup": True}


@app.get("/messages", response_model=list[StoredMessage])
def list_messages(
    _=Depends(_require_key),
    limit: int = Query(50, ge=1, le=500),
    offset: int = Query(0, ge=0),
    thread: Optional[str] = None,
) -> list[StoredMessage]:
    sql = "SELECT * FROM messages"
    params: list = []
    if thread:
        sql += " WHERE thread_id = ?"
        params.append(_normalize_thread_key(thread))
    sql += " ORDER BY received_at DESC LIMIT ? OFFSET ?"
    params.extend([limit, offset])
    with _db() as conn:
        rows = conn.execute(sql, params).fetchall()
    return [StoredMessage(**{**dict(r), "is_group": bool(r["is_group"])}) for r in rows]


@app.get("/threads")
def threads(_=Depends(_require_key), limit: int = Query(50, ge=1, le=500)) -> list[dict]:
    with _db() as conn:
        rows = conn.execute(
            """
            SELECT thread_id,
                   MAX(received_at) AS last_at,
                   COUNT(*)         AS n,
                   (SELECT body FROM messages m2 WHERE m2.thread_id = m1.thread_id ORDER BY received_at DESC LIMIT 1) AS last_body,
                   (SELECT sender FROM messages m2 WHERE m2.thread_id = m1.thread_id ORDER BY received_at DESC LIMIT 1) AS last_sender
            FROM messages m1
            GROUP BY thread_id
            ORDER BY last_at DESC
            LIMIT ?
            """,
            (limit,),
        ).fetchall()
    return [dict(r) for r in rows]


# ---- rendered view --------------------------------------------------------

_INDEX_HTML = """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Pono iMessage</title>
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <style>
    body { font: 14px/1.4 system-ui, sans-serif; margin: 0; background: #111; color: #eee; }
    header { padding: 12px 16px; background: #222; border-bottom: 1px solid #333; }
    header h1 { margin: 0; font-size: 16px; font-weight: 600; }
    #gate { padding: 40px; max-width: 420px; }
    #gate input { width: 100%; padding: 8px; background: #222; color: #eee; border: 1px solid #444; border-radius: 4px; }
    #gate button { margin-top: 8px; padding: 8px 16px; background: #147efb; color: white; border: 0; border-radius: 4px; cursor: pointer; }
    main { display: grid; grid-template-columns: 280px 1fr; height: calc(100vh - 46px); }
    #threads { overflow-y: auto; border-right: 1px solid #333; }
    #threads .t { padding: 10px 14px; border-bottom: 1px solid #222; cursor: pointer; }
    #threads .t:hover { background: #1a1a1a; }
    #threads .t.active { background: #2a2a2a; }
    #threads .name { font-weight: 600; }
    #threads .preview { color: #888; font-size: 12px; margin-top: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    #messages { overflow-y: auto; padding: 16px; display: flex; flex-direction: column-reverse; }
    .m { margin: 4px 0; max-width: 70%; padding: 8px 12px; border-radius: 14px; }
    .m.in { background: #2a2a2a; align-self: flex-start; }
    .m.out { background: #147efb; align-self: flex-end; color: white; }
    .m .meta { font-size: 11px; color: #999; margin-bottom: 2px; }
    .empty { padding: 24px; color: #888; }
  </style>
</head>
<body>
  <header><h1>pono-imessage - phase 1 (receive-only)</h1></header>
  <div id="app"></div>
  <script>
    // Secret lives ONLY in sessionStorage (cleared on tab close) and never in URL.
    // One paste per tab session. No persistence to disk.
    const SS_KEY = 'pono-key';
    let activeThread = null;

    function getKey() { return sessionStorage.getItem(SS_KEY); }
    function setKey(v) { sessionStorage.setItem(SS_KEY, v); render(); }
    function clearKey() { sessionStorage.removeItem(SS_KEY); render(); }

    function render() {
      const app = document.getElementById('app');
      if (!getKey()) {
        app.innerHTML = `
          <div id="gate">
            <p>Paste your <code>PONO_IMESSAGE_KEY</code>. It stays in this tab's session only.</p>
            <input type="password" id="k" autocomplete="off" placeholder="PONO_IMESSAGE_KEY">
            <button onclick="setKey(document.getElementById('k').value)">Unlock</button>
          </div>`;
      } else {
        app.innerHTML = `
          <main>
            <div id="threads"><div class="empty">loading...</div></div>
            <div id="messages"><div class="empty">select a thread</div></div>
          </main>`;
        loadThreads();
        if (!window._pono_poll) window._pono_poll = setInterval(loadThreads, 5000);
      }
    }

    async function fetchJson(path) {
      const r = await fetch(path, { headers: { 'X-Pono-Key': getKey() } });
      if (r.status === 401) { clearKey(); throw new Error('bad key'); }
      if (!r.ok) throw new Error(r.status + '');
      return r.json();
    }

    async function loadThreads() {
      try {
        const ts = await fetchJson('/threads');
        const el = document.getElementById('threads');
        if (!el) return;
        if (!ts.length) { el.innerHTML = '<div class="empty">no messages yet</div>'; return; }
        el.innerHTML = ts.map(t => `
          <div class="t ${t.thread_id===activeThread?'active':''}" data-tid="${encodeURIComponent(t.thread_id)}">
            <div class="name">${escapeHtml(t.last_sender)}</div>
            <div class="preview">${escapeHtml(t.last_body || '').slice(0,80)}</div>
          </div>`).join('');
        for (const node of el.querySelectorAll('.t')) {
          node.onclick = () => openThread(decodeURIComponent(node.dataset.tid));
        }
      } catch (e) { /* render() already cleared key on 401 */ }
    }

    async function openThread(tid) {
      activeThread = tid;
      loadThreads();
      try {
        const msgs = await fetchJson('/messages?thread=' + encodeURIComponent(tid) + '&limit=200');
        const el = document.getElementById('messages');
        if (!msgs.length) { el.innerHTML = '<div class="empty">empty thread</div>'; return; }
        el.innerHTML = msgs.map(m => `
          <div class="m ${m.direction}">
            <div class="meta">${escapeHtml(m.sender)} - ${m.received_at.slice(0,19).replace('T',' ')}</div>
            <div>${escapeHtml(m.body)}</div>
          </div>`).join('');
      } catch (e) {}
    }

    function escapeHtml(s) { return String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }

    render();
  </script>
</body>
</html>
"""


@app.get("/", response_class=HTMLResponse)
def index() -> str:
    return _INDEX_HTML


# ---- entrypoint -----------------------------------------------------------

if __name__ == "__main__":
    import uvicorn

    # Custom log config: keep default format but let our _SecretRedactFilter
    # attach to uvicorn's loggers. The redactor is already attached above;
    # the filter runs on any log record that ever carries the shared secret.
    LOG.info("listening on http://%s:%d  db=%s", HOST, PORT, DB_PATH)
    uvicorn.run(app, host=HOST, port=PORT, log_level="info")
