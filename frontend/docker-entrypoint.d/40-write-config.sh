#!/bin/sh
set -eu

: "${API_BASE_URL:=http://localhost:8080}"

cat > /usr/share/nginx/html/config.js <<EOF
window.__APP_CONFIG__ = { apiBaseUrl: "${API_BASE_URL}" };
EOF
