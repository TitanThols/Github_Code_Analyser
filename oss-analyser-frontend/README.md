# DepScan frontend

React 19 + TypeScript SPA built with Vite, Tailwind CSS v4, TanStack Query, Recharts and SignalR. See the [root README](../README.md) for the full project.

```bash
npm ci
npm run dev            # http://localhost:5173 (proxies /api and /hubs to http://localhost:5100)
npm run test:coverage  # Vitest + React Testing Library
npm run lint && npm run typecheck && npm run build
```

Set `VITE_API_PROXY_TARGET` to proxy to a different API origin during development. Set `VITE_API_BASE_URL` at build time only when the SPA is hosted separately from the API.

## Structure

```
src/
  api/          Typed REST endpoints
  lib/          fetch client (ProblemDetails-aware), formatting, filters, CSV, GitHub URL validation
  hooks/        TanStack Query hooks, SignalR live alerts, theme
  context/      Auth and toast providers
  components/   ui/ primitives · charts/ · scan/ · repository/ · layout/
  pages/        Login, Dashboard, Scan, Repositories, Repository detail
```

Design tokens live in `src/index.css`. Light and dark values are defined separately, and the severity palette was checked for colour-vision-deficiency separation in both modes. Severity is always shown with a text label as well as colour.
