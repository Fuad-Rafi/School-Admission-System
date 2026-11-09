# syntax=docker/dockerfile:1

FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build


FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-write-config.sh /docker-entrypoint.d/40-write-config.sh
RUN chmod +x /docker-entrypoint.d/40-write-config.sh

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
