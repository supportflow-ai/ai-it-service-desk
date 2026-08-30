# ============================================
# Frontend — Multi-stage Dockerfile
# Build with Vite, serve static via NGINX
# ============================================

# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

COPY src/frontend/service-desk-web/package.json src/frontend/service-desk-web/package-lock.json* ./
RUN npm install

COPY src/frontend/service-desk-web/ .
RUN npm run build

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html

# SPA fallback: route all paths to index.html
RUN echo 'server { \
    listen 80; \
    server_name _; \
    root /usr/share/nginx/html; \
    index index.html; \
    location / { \
        try_files $uri $uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf

EXPOSE 80
