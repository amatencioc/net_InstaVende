????????????????????????????????????????????????????????????????????????????????
?           INSTAVENDE — CONTEXTO DINÁMICO DE SESIÓN v1.0                    ?
?           Inyectar en cada llamada a la API de OpenAI                      ?
????????????????????????????????????????????????????????????????????????????????

[TEMPORAL]
Fecha: {{CURRENT_DATE}}
Hora actual: {{CURRENT_HOUR}}:{{CURRENT_MINUTE}}
Día de semana: {{DAY_OF_WEEK}}
Es fin de semana: {{IS_WEEKEND}}
Dentro del horario de atención: {{IS_WITHIN_BUSINESS_HOURS}}

[USUARIO]
Nombre: {{USER_NAME | "No disponible"}}
ID: {{USER_ID}}
País: {{USER_COUNTRY}}
Canal: {{CHANNEL}}
Es cliente recurrente: {{IS_RETURNING_USER}}
Segmento: {{USER_SEGMENT | "general"}}
Total de compras anteriores: {{TOTAL_PURCHASES | 0}}
Última compra: {{LAST_PURCHASE_SUMMARY | "Sin compras previas"}}
Último tema de conversación: {{LAST_TOPIC | "Ninguno"}}
Preferencias conocidas: {{USER_PREFERENCES | "Sin preferencias registradas"}}

[ESTADO DE LA CONVERSACIÓN ACTUAL]
Intento de negociación actual: {{NEGOTIATION_ATTEMPT | 0}} de 4
Productos discutidos en esta sesión: {{PRODUCTS_DISCUSSED | "Ninguno"}}
Sentimiento detectado: {{DETECTED_SENTIMENT | "neutral"}}

[CONFIGURACIÓN DE NEGOCIACIÓN]
Descuento nivel 1 (valor agregado): {{DISCOUNT_LEVEL_1}}%
Descuento nivel 2 (medio): {{DISCOUNT_LEVEL_2}}%
Descuento nivel 3 (máximo autorizado): {{DISCOUNT_LEVEL_3}}%
Descuento máximo absoluto: {{MAX_DISCOUNT_PERCENT}}%
Margen mínimo permitido: {{MIN_MARGIN_PERCENT}}%
Envío gratis desde: ${{FREE_SHIPPING_THRESHOLD}}
Descuento por bundle/combo: {{BUNDLE_DISCOUNT}}%
Descuento por fidelidad (recurrente): {{LOYALTY_DISCOUNT}}%

[PROMOCIONES VIGENTES]
{{ACTIVE_PROMOTIONS | "Sin promociones activas actualmente."}}

[POLÍTICAS]
Devoluciones: {{RETURN_POLICY}}
Garantía: {{WARRANTY_POLICY}}
Tiempos de envío: {{SHIPPING_TIMES}}
Métodos de pago: {{PAYMENT_METHODS}}

[DISPONIBILIDAD DE AGENTES HUMANOS]
Agentes disponibles ahora: {{AGENTS_AVAILABLE}}
Tiempo estimado de espera: {{ESTIMATED_WAIT_TIME}} minutos
Horario de atención humana: {{BUSINESS_HOURS}}

[EMPRESA]
Nombre: {{COMPANY_NAME}}
Sector/Rubro: {{BUSINESS_SECTOR}}
Sitio web: {{COMPANY_WEBSITE}}
Teléfono: {{COMPANY_PHONE}}
Email: {{COMPANY_EMAIL}}
Redes sociales: {{SOCIAL_MEDIA}}
