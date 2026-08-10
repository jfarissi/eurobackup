# ============================================================
# Dockerfile — Frontend Angular ERP
# ============================================================

# Étape 1: Build
FROM node:20-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration production

# Étape 2: Serveur Nginx
FROM nginx:alpine
COPY --from=builder /app/dist/erp-client/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
