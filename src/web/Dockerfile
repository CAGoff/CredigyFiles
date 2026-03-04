# Stage 1: Build
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# VITE_* env vars must be set at build time (baked into JS bundle)
ARG VITE_API_BASE_URL
ARG VITE_AAD_CLIENT_ID
ARG VITE_AAD_TENANT_ID
ARG VITE_AAD_API_CLIENT_ID

RUN npm run build

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
