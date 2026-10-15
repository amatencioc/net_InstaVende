"""Prueba de humo E2E contra la API real (SQL Server LocalDB). Uso: python tests/e2e/smoke_test.py http://localhost:5221 (API con BD vacía, AutoStart de wa-client desactivado)"""
import hashlib, hmac, http.cookiejar, json, sys, time, urllib.error, urllib.request, uuid
from datetime import datetime, timedelta, timezone

BASE = sys.argv[1].rstrip("/")
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
results = []


def cookie(name):
    return next((c.value for c in jar if c.name == name), None)


def call(method, path, body=None, expect=(200, 201, 204), raw=None, headers=None):
    h = {"Accept": "application/json", "X-Requested-With": "XMLHttpRequest"}
    data = None
    if raw is not None:
        data = raw
        h["Content-Type"] = "application/json"
    elif body is not None:
        data = json.dumps(body).encode()
        h["Content-Type"] = "application/json"
    if method not in ("GET", "HEAD") and cookie("XSRF-TOKEN"):
        h["X-XSRF-TOKEN"] = urllib.parse.unquote(cookie("XSRF-TOKEN"))
    h.update(headers or {})
    req = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with opener.open(req, timeout=60) as r:
            status, text = r.status, r.read().decode("utf-8-sig")
    except urllib.error.HTTPError as e:
        status, text = e.code, e.read().decode("utf-8-sig", "replace")
    ok = status in (expect if isinstance(expect, tuple) else (expect,))
    results.append((ok, method, path, status))
    mark = "OK " if ok else "FAIL"
    print(f"{mark} {method:6} {path} -> {status}" + ("" if ok else f"\n      {text[:400]}"))
    try:
        return json.loads(text) if text else None
    except json.JSONDecodeError:
        return text


import urllib.parse

# ── 1. Autenticación ─────────────────────────────────────────────────────────
call("GET", "/api/auth/session", expect=401)
call("GET", "/api/auth/csrf", expect=204)
email = f"e2e-{uuid.uuid4().hex[:8]}@test.pe"
call("POST", "/api/auth/register", {"firstName": "Ana", "lastName": "Pérez", "email": email, "password": "corta"}, expect=400)
session = call("POST", "/api/auth/register", {"firstName": "Ana", "lastName": "Pérez", "email": email,
                                              "password": "Secreta123", "businessName": "Tienda E2E"})
bid = session["business"]["id"]
call("GET", "/api/auth/session")
call("POST", "/api/products", {"name": "Sin CSRF"}, expect=400, headers={"X-XSRF-TOKEN": "malo"})

# ── 2. Configuración del negocio ─────────────────────────────────────────────
call("PUT", "/api/settings/business", None, expect=415)  # sin cuerpo JSON: 415
biz = call("GET", "/api/settings/business")
cat = call("POST", "/api/product-categories", {"name": "Zapatillas"}, expect=(200, 201))
cat_id = cat["id"] if isinstance(cat, dict) else None
p1 = call("POST", "/api/products", {"name": "Zapatilla Runner", "description": "Ligera para correr", "sku": "RUN-01",
                                    "price": 199.9, "priceOriginal": 249.9, "stock": 10, "categoryId": cat_id,
                                    "isActive": True, "isFeatured": True,
                                    "variants": [{"attributeName": "Talla", "attributeValue": "42", "priceModifier": 0, "stock": 4, "sku": None, "isActive": True}]},
          expect=(200, 201))
call("POST", "/api/products", {"name": "Duplicado", "description": None, "sku": "RUN-01", "price": 1, "priceOriginal": None,
                               "stock": 1, "categoryId": None}, expect=409)
call("POST", "/api/products", {"name": "Medias deportivas", "description": "Pack x3", "sku": "MED-03", "price": 29.9,
                               "priceOriginal": None, "stock": 50, "categoryId": cat_id, "isActive": True, "isFeatured": False},
     expect=(200, 201))
plist = call("GET", "/api/products?search=runner&page=1&pageSize=10")
assert plist["totalCount"] == 1, plist
call("GET", f"/api/products/{p1['id']}")

call("PUT", "/api/seller/profile", {"vendorName": "Favio", "vendorGender": "Masculino", "country": "Perú",
                                    "businessDescription": "Zapatillas deportivas originales", "targetAudience": "Corredores",
                                    "rules": "SIEMPRE confirma la talla.", "communicationStyle": "Cercano", "salesStyle": "Consultivo",
                                    "responseLength": 3, "useEmojis": True, "emojiPalette": ["👟", "🔥"], "useOpeningPunctuation": True,
                                    "wordsToAvoid": ["barato"], "welcomeMessage": "¡Hola! Soy Favio 👟", "purchaseConfirmationMessage": "¡Gracias por tu compra!",
                                    "humanHandoffSituations": ["Reclamos"], "autoPauseOnHandoff": True, "handoffMessage": "Te paso con un asesor"})
rules = call("GET", "/api/seller/rules")
call("POST", "/api/payment-methods", {"type": "DigitalWallet", "name": "Yape", "instructions": "Yapea al 999 888 777",
                                      "accountNumber": "999888777", "accountAlias": "Ana Pérez", "paymentLink": None, "isActive": True},
     expect=(200, 201))
call("POST", "/api/payment-methods", {"type": "Other", "name": "Link", "instructions": None, "accountNumber": None,
                                      "accountAlias": None, "paymentLink": "javascript:alert(1)"}, expect=400)
call("POST", "/api/delivery-zones", {"name": "Miraflores", "cost": 10, "description": "24h", "isActive": True}, expect=(200, 201))
call("POST", "/api/knowledge", {"title": "¿Hacen envíos a provincia?", "content": "Sí, por Olva en 3-5 días.", "category": "Envios"},
     expect=(200, 201))
call("POST", "/api/seller/test", {"message": "#catalogo"})

# ── 3. Canal WhatsApp Cloud + mensajes entrantes firmados ────────────────────
app_secret = "app-secret-e2e"
call("PUT", "/api/channels/WhatsApp/meta-config", {"phoneNumberId": "1234567890", "wabaId": None, "pageId": None,
                                                    "instagramAccountId": None, "accessToken": "EAAG-fake-token",
                                                    "appSecret": app_secret, "isActive": True})
channels = call("GET", "/api/channels")


def wa_inbound(text, msg_id):
    payload = {"entry": [{"changes": [{"value": {
        "contacts": [{"wa_id": "51999111222", "profile": {"name": "Cliente E2E"}}],
        "messages": [{"from": "51999111222", "id": msg_id, "type": "text", "text": {"body": text}}]}}]}]}
    raw = json.dumps(payload, ensure_ascii=False).encode()
    sig = "sha256=" + hmac.new(app_secret.encode(), raw, hashlib.sha256).hexdigest()
    return call("POST", f"/api/webhooks/whatsapp/{bid}", raw=raw, headers={"X-Hub-Signature-256": sig})


call("POST", f"/api/webhooks/whatsapp/{bid}", raw=b'{"entry":[]}', headers={"X-Hub-Signature-256": "sha256=00"}, expect=401)
for i, text in enumerate(["#catalogo", "agregar 1", "agregar 1", "#pedido"]):
    wa_inbound(text, f"wamid.e2e.{i}")
    time.sleep(2.5)
wa_inbound("#catalogo", "wamid.e2e.0")  # duplicado: debe ignorarse
time.sleep(3)

convs = call("GET", "/api/conversations?status=All&page=1&pageSize=30")
conv = convs["items"][0]
msgs = call("GET", f"/api/conversations/{conv['id']}/messages?limit=50")
inbound = [m for m in msgs["items"] if m["direction"] == "Inbound"]
print(f"   mensajes: {len(msgs['items'])} (entrantes {len(inbound)}), estados salientes:",
      sorted({m['deliveryStatus'] for m in msgs['items'] if m['direction'] == 'Outbound'}))
results.append((len(inbound) == 4, "CHECK", "dedupe entrantes (4 esperados)", len(inbound)))
call("GET", "/api/conversations/counts")
call("POST", f"/api/conversations/{conv['id']}/read", expect=(200, 204))
call("POST", f"/api/conversations/{conv['id']}/status", {"status": "HumanActive"}, expect=(200, 204))
call("POST", f"/api/conversations/{conv['id']}/messages", {"text": "Hola, te escribe un asesor"}, expect=(200, 201))

# ── 4. Pedidos ───────────────────────────────────────────────────────────────
board = call("GET", "/api/orders/board?tab=Nuevos")
cards = [c for col in board["columns"] for c in col["orders"]] if isinstance(board, dict) and "columns" in board else []
results.append((len(cards) == 1, "CHECK", "pedido creado por el bot en el tablero", len(cards)))
if cards:
    order_id = cards[0]["id"]
    detail = call("GET", f"/api/orders/{order_id}")
    print(f"   pedido {detail.get('orderNumber')} total={detail.get('total')} items={len(detail.get('items', []))}")
    results.append((abs(detail.get("total", 0) - 399.8) < 0.01, "CHECK", "total del pedido = 2 x 199.90", detail.get("total")))
    call("POST", f"/api/orders/{order_id}/move", {"subStatus": "EnPreparacion", "notifyCustomer": True})
    call("GET", "/api/orders/export?tab=EnCurso")

# ── 5. Analítica, recordatorios, equipo ──────────────────────────────────────
dash = call("GET", "/api/dashboard")
call("GET", "/api/metrics?period=30d")
contacts = call("GET", "/api/reminders/contacts?search=Cliente")
contact_id = contacts[0]["id"] if isinstance(contacts, list) and contacts else None
when = (datetime.now(timezone.utc) + timedelta(hours=2)).isoformat().replace("+00:00", "Z")
call("POST", "/api/reminders", {"contactId": contact_id, "message": "¿Sigues interesado en las zapatillas?", "scheduledAt": when},
     expect=(200, 201))
call("GET", "/api/reminders?status=Pending&page=1")
call("GET", "/api/reminder-templates")
call("POST", "/api/team/invitations", {"email": f"socio-{uuid.uuid4().hex[:6]}@test.pe", "role": "Member"}, expect=(200, 201))
call("GET", "/api/team")
call("GET", "/api/channels/whatsapp/session", expect=(200, 409))
call("POST", "/api/auth/logout", expect=204)
call("GET", "/api/products", expect=401)

failed = [r for r in results if not r[0]]
print(f"\n{len(results) - len(failed)}/{len(results)} comprobaciones correctas")
for f in failed:
    print("  FALLO:", f)
sys.exit(1 if failed else 0)
