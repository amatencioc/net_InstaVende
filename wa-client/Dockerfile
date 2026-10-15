# syntax=docker/dockerfile:1.7
# wa-client: sesiones de WhatsApp Web (QR) por negocio, con Chromium del sistema.
FROM node:22-bookworm-slim
RUN apt-get update \
 && apt-get install -y --no-install-recommends chromium fonts-noto-color-emoji ca-certificates \
 && rm -rf /var/lib/apt/lists/*
ENV PUPPETEER_SKIP_DOWNLOAD=true \
    PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium \
    WA_HOST=0.0.0.0 \
    WA_PORT=3001 \
    WA_SESSION_DIR=/data/sessions \
    NODE_ENV=production
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev --no-audit --no-fund
COPY index.js ./
RUN mkdir -p /data/sessions && chown -R node:node /data
USER node
VOLUME ["/data/sessions"]
EXPOSE 3001
CMD ["node", "index.js"]
