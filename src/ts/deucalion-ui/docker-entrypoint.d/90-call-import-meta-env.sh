#!/bin/sh

/app/import-meta-env-alpine -x /app/.env -p /usr/share/nginx/html/index.html || exit 1
