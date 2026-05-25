FROM node:25-alpine AS build
WORKDIR /repo

RUN npm install -g pnpm@10.33.4

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/docs-web/package.json apps/docs-web/
COPY packages/shared-ui/package.json packages/shared-ui/
COPY apps/docs-web apps/docs-web
COPY packages/shared-ui packages/shared-ui

RUN pnpm --dir apps/docs-web install --frozen-lockfile && pnpm --dir apps/docs-web build

FROM nginx:1.27-alpine
COPY infra/compose/frontend-nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/apps/docs-web/dist /usr/share/nginx/html
