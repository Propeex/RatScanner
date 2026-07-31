# Fallback API data

Release builds bundle item snapshots from `json.tarkov.dev` so RatScanner can start when the GraphQL API is unavailable. The bundled files are used only as a startup fallback; normal GraphQL refreshes remain authoritative when available.
