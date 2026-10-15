FROM node:22-alpine
WORKDIR /app
COPY relay.js .
# Runs as root: the Fly volume at /data is mounted root-owned.
CMD ["node", "relay.js"]
